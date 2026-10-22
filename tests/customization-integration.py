"""RBAC and customization regression on isolated localhost:5084.
Use WORKSPACE_TEST_EMAIL/PASSWORD (system role manager) and WORKSPACE_OTHER_EMAIL/PASSWORD (ordinary account).
Creates disposable projects/roles. Never targets the main database/API.
"""
import json
import os
import urllib.error
import urllib.request
import uuid
from pathlib import Path

BASE = "http://localhost:5084"
results = []


def request(method, path, body=None, token=None, expected=200, raw=None, content_type=None):
    headers = {"Content-Type": content_type or "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    payload = raw if raw is not None else json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=payload, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            status, data = response.status, response.read()
    except urllib.error.HTTPError as error:
        status, data = error.code, error.read()
    try:
        value = json.loads(data) if data else None
    except (ValueError, UnicodeDecodeError):
        value = data
    results.append({"method": method, "path": path, "status": status, "expected": expected})
    print(method, path, status)
    assert status == expected, repr(value)[:1000]
    return value


def login(prefix="WORKSPACE_TEST"):
    return request("POST", "/api/v1/auth/login", {
        "email": os.environ[prefix + "_EMAIL"], "password": os.environ[prefix + "_PASSWORD"]
    })



owner, other = login(), login("WORKSPACE_OTHER")
token, outsider = owner["accessToken"], other["accessToken"]
tag = uuid.uuid4().hex[:7]
users = request("GET", "/api/v1/admin/users", token=token)
assert any(u["email"] == os.environ["WORKSPACE_OTHER_EMAIL"] for u in users)
assert all("passwordHash" not in u and "securityStamp" not in u for u in users)
assert any(u["roles"] for u in users)
request("GET", "/api/v1/admin/users", token=outsider, expected=403)
permissions = request("GET", "/api/v1/permissions", token=token)["items"]
read_id = next(p["permissionId"] for p in permissions if p["code"] == "project.read")
update_id = next(p["permissionId"] for p in permissions if p["code"] == "issue.update")
role = request("POST", "/api/v1/roles", {"name": "Audit"+tag, "scope": "Project", "permissionIds": [read_id]}, token, 201)
rid = role["roleId"]
def saved_role():
    return next(r for r in request("GET", "/api/v1/roles", token=token)["items"] if r["roleId"] == rid)
assert saved_role()["permissionIds"] == [read_id]
request("PUT", f"/api/v1/roles/{rid}/permissions", {"permissionIds": [read_id, update_id]}, outsider, 403)
request("PUT", f"/api/v1/roles/{rid}/permissions", {"permissionIds": [read_id, update_id]}, token)
assert set(saved_role()["permissionIds"]) == {read_id, update_id}
request("PUT", f"/api/v1/roles/{rid}/permissions", {"permissionIds": ["project.read"]}, token, 400)
assert set(saved_role()["permissionIds"]) == {read_id, update_id}
request("PUT", f"/api/v1/roles/{rid}/permissions", {"permissionIds": [str(uuid.uuid4())]}, token, 400)
assert set(saved_role()["permissionIds"]) == {read_id, update_id}
request("PUT", f"/api/v1/roles/{rid}/permissions", {"permissionIds": []}, token)
assert saved_role()["permissionIds"] == []
org = request("POST", "/api/v1/organizations", {"name": "Customization "+tag, "slug": "custom-"+tag}, token, 201)
project = request("POST", "/api/v1/projects", {"orgId": org["id"], "name": "Customization", "projectKey": "C"+tag.upper()}, token, 201)
pid = project["id"]
url = "/api/v1/projects/"+pid
type_body = {"name": "Risk "+tag, "icon": "warning", "color": "#6366F1"}
request("POST", url+"/issue-types", type_body, outsider, 403)
custom = request("POST", url+"/issue-types", type_body, token, 201)
types = request("GET", url+"/issue-types", token=token)
assert any(t["id"] == custom["id"] and t["name"] == type_body["name"] for t in types)
request("POST", url+"/issue-types", type_body, token, 409)
request("POST", url+"/issue-types", {**type_body, "color": "indigo"}, token, 400)
request("POST", url+"/issue-types", {**type_body, "name": "   "}, token, 400)
workspace = request("GET", url+"/workspace", token=token)
initial = next(s for s in workspace["statuses"] if s["isInitial"])
issue = request("POST", url+"/issues", {"title": "Custom risk", "issueType": type_body["name"], "statusName": initial["name"]}, token)
assert request("GET", "/api/v1/issues/"+issue["id"], token=token)["issueType"] == type_body["name"]
original = {s["id"] for s in workspace["statuses"]}
transitions_before = request("GET", url+"/workflow/transitions", token=token)
request("POST", url+"/workflow/template", {"templateId": "agile-qa"}, outsider, 403)
request("POST", url+"/workflow/template", {"templateId": "missing"}, token, 400)
statuses = request("POST", url+"/workflow/template", {"templateId": "agile-qa"}, token)
assert original.issubset({s["id"] for s in statuses})
assert len([s for s in statuses if s["isInitial"]]) == 1
qa = next(s for s in statuses if s["name"] == "QA Testing")
saved = request("GET", url+"/statuses", token=token)
assert any(s["id"] == qa["id"] for s in saved)
transitions = request("GET", url+"/workflow/transitions", token=token)
assert all(any(t["id"] == old["id"] and t["requiredPermissionCode"] == old["requiredPermissionCode"] for t in transitions) for old in transitions_before)
assert any(t["toStatusId"] == qa["id"] for t in transitions)
boards = request("GET", url+"/boards", token=token)
for board in boards:
    columns = request("GET", "/api/boards/"+board["id"]+"/columns", token=token)
    assert any(c["statusId"] == qa["id"] for c in columns)
again = request("POST", url+"/workflow/template", {"templateId": "agile-qa"}, token)
assert {s["id"] for s in again} == {s["id"] for s in statuses}
assert len(request("GET", url+"/workflow/transitions", token=token)) == len(transitions)
assert request("GET", "/api/v1/issues/"+issue["id"], token=token)["statusId"] == initial["id"]
# Exercise all published templates, including idempotence and custom-status preservation.
for template in ["scrum-std", "kanban-simple", "enterprise"]:
    request("POST", url+"/workflow/template", {"templateId": template}, token)
    current = request("GET", url+"/statuses", token=token)
    reapplied = request("POST", url+"/workflow/template", {"templateId": template}, token)
    assert {s["id"] for s in current} == {s["id"] for s in reapplied}
    assert len([s for s in reapplied if s["isInitial"]]) == 1
# A project member with read-only permissions still cannot customize the project.
request("POST", url+"/members/invite", {"email": os.environ["WORKSPACE_OTHER_EMAIL"], "roleName": "Viewer"}, token, 201)
request("GET", url+"/issue-types", token=outsider)
request("POST", url+"/issue-types", {**type_body, "name": "Forbidden"}, outsider, 403)
request("POST", url+"/workflow/template", {"templateId": "scrum-std"}, outsider, 403)
out = Path(os.environ.get("TEMP", "/tmp")) / "projectmgmt-audit" / "customization-regression.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"checks": len(results), "results": results}, indent=2), encoding="utf-8")
print("PASS:", len(results), "HTTP checks")
