"""Regression smoke against an isolated MySQL-backed API on localhost:5084.

Provide WORKSPACE_TEST_EMAIL/PASSWORD and WORKSPACE_OTHER_EMAIL/PASSWORD for
two verified accounts. The second account must not have system/admin access.
Creates its own organization/project; never runs against the normal :5083 API.
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
org = request("POST", "/api/v1/organizations", {"name": "Workspace regression " + tag, "slug": "test-" + tag}, token, 201)
project = request("POST", "/api/v1/projects", {"orgId": org["id"], "name": "Workspace regression", "projectKey": "T" + tag.upper()}, token, 201)
pid = project["id"]
project_url = "/api/v1/projects/" + pid
request("GET", project_url, token=outsider, expected=403)
request("PUT", project_url, {"name": "Unauthorized edit"}, outsider, 403)
request("DELETE", project_url, token=outsider, expected=403)
assert not any(p["id"] == pid for p in request("GET", "/api/v1/projects", token=outsider))
workspace = request("GET", project_url + "/workspace", token=token)
status_name = next(s["name"] for s in workspace["statuses"] if s["isInitial"])
sprint = request("POST", project_url + "/sprints", {"name": "Regression Sprint", "status": "future", "startDate": "2026-10-20", "endDate": "2026-11-03"}, token)
epic = request("POST", project_url + "/issues", {"title": "Persistent epic", "issueType": "Epic", "priority": "Medium", "statusName": status_name}, token)
task = request("POST", project_url + "/issues", {"title": "Persistent task", "issueType": "Task", "priority": "High", "statusName": status_name, "storyPoints": 3, "sprintId": sprint["id"], "epicId": epic["id"], "labels": ["regression"]}, token)
issue_url = "/api/v1/issues/" + task["id"]
subtask = request("POST", project_url + "/issues", {"title": "Persistent subtask", "issueType": "Sub-task", "parentId": task["id"], "statusName": status_name}, token)
request("POST", issue_url + "/comments", {"content": "Persistent comment"}, token)
request("POST", issue_url + "/criteria", {"content": "Persists after reload"}, token)
saved = request("GET", issue_url, token=token)
assert saved["comments"][0]["content"] == "Persistent comment"
assert saved["epicId"] == epic["id"] and saved["labels"] == ["regression"]
criterion = saved["criteria"][0]
request("PUT", "/api/v1/criteria/" + criterion["id"], {"content": criterion["content"], "isMet": True}, token)
saved["title"] = "Persisted updated title"
request("PUT", issue_url, saved, token)
assert request("GET", issue_url, token=token)["title"] == saved["title"]
transitions = request("GET", project_url + "/workflow/transitions", token=token)
target = next(t for t in transitions if t["fromStatusId"] == saved["statusId"])
saved["statusName"] = target["toStatusName"]
request("PUT", issue_url, saved, token)
request("PUT", issue_url, saved, outsider, 403)
request("GET", issue_url, token=outsider, expected=403)
boundary = "workspace-boundary"
payload = ('--' + boundary + '\r\nContent-Disposition: form-data; name="file"; filename="regression.txt"\r\nContent-Type: text/plain\r\n\r\npersistent file\r\n--' + boundary + '--\r\n').encode()
attachment = request("POST", issue_url + "/attachments", token=token, raw=payload, content_type="multipart/form-data; boundary=" + boundary)
download_url = "/api/v1/attachments/" + attachment["id"]
assert request("GET", download_url, token=token) == b"persistent file"
request("GET", download_url, token=outsider, expected=403)
request("DELETE", download_url, token=outsider, expected=403)
workspace = request("GET", project_url + "/workspace", token=token)
assert len(workspace["workItems"]) == 3 and len(workspace["sprints"]) == 1
assert next(i for i in workspace["workItems"] if i["id"] == task["id"])["criteria"][0]["isMet"]
order = [subtask["id"], task["id"], epic["id"]]
request("PUT", project_url + "/issues/order", {"orderedIds": order}, token, 204)
assert [i["id"] for i in request("GET", project_url + "/workspace", token=token)["workItems"]] == order
request("PUT", project_url + "/issues/order", {"orderedIds": order}, outsider, 403)
sprint["status"] = "active"
request("PUT", "/api/v1/sprints/" + sprint["id"], sprint, token)
second_sprint = request("POST", project_url + "/sprints", {"name": "Second Sprint", "status": "future"}, token)
second_sprint["status"] = "active"
request("PUT", "/api/v1/sprints/" + second_sprint["id"], second_sprint, token, 409)
sprint["status"] = "completed"
request("PUT", "/api/v1/sprints/" + sprint["id"], sprint, token)
request("POST", project_url + "/members/invite", {"email": os.environ["WORKSPACE_OTHER_EMAIL"], "roleName": "Viewer"}, token, 201)
request("GET", project_url + "/workspace", token=outsider)
request("PUT", issue_url, saved, outsider, 403)
request("POST", issue_url + "/comments", {"content": "Viewer cannot write"}, outsider, 403)
request("DELETE", project_url + "/members/" + other["userId"], token=token)
request("GET", issue_url, token=outsider, expected=403)
sessions = request("GET", "/api/v1/users/me/sessions", token=token)
assert any(s["isCurrent"] for s in sessions)
second = login()
session = next(s for s in request("GET", "/api/v1/users/me/sessions", token=second["accessToken"]) if s["isCurrent"])
request("DELETE", "/api/v1/users/me/sessions/" + session["id"], token=outsider, expected=404)
request("DELETE", "/api/v1/users/me/sessions/" + session["id"], token=token)
request("GET", "/api/v1/users/me", token=second["accessToken"], expected=401)
request("POST", "/api/v1/auth/refresh-token", {"refreshToken": second["refreshToken"]}, expected=401)
request("DELETE", download_url, token=token, expected=204)
request("DELETE", issue_url, token=token, expected=204)
request("GET", "/api/v1/issues/" + subtask["id"], token=token, expected=404)
output = Path(os.environ.get("TEMP", ".tmp")) / "projectmgmt-audit" / "workspace-regression.json"
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({"results": results, "projectId": pid, "checks": len(results)}, indent=2), encoding="utf-8")
print("PASS:", len(results), "HTTP checks; evidence:", output)
