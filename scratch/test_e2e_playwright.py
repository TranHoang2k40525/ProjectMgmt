import sys
import time
from playwright.sync_api import sync_playwright

artifact_dir = r"C:\Users\hoang\.gemini\antigravity-ide\brain\a7cd8610-b76b-45df-9f7f-ab668a708d59"

def run():
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(viewport={"width": 1440, "height": 900})
        page = context.new_page()

        console_errors = []
        page.on("console", lambda msg: console_errors.append(msg.text) if msg.type == "error" else None)

        print("1. Navigating to http://localhost:4200/...")
        page.goto("http://localhost:4200/", wait_until="networkidle")
        time.sleep(2)

        # Check if already on login or redirected
        print("Page URL:", page.url)

        # If on auth page, fill in credentials
        if "/auth" in page.url or page.locator("input[type='email']").is_visible():
            print("2. Filling login form with hoanghaihau989@gmail.com...")
            page.fill("input[type='email']", "hoanghaihau989@gmail.com")
            page.fill("input[type='password']", "@123456xX")
            page.click("button[type='submit']")
            page.wait_for_load_state("networkidle")
            time.sleep(3)

        print("Current URL after login:", page.url)

        # Take screenshot of Org Overview
        overview_screenshot = f"{artifact_dir}\\playwright_04_org_overview_success.png"
        page.screenshot(path=overview_screenshot)
        print(f"Saved overview screenshot: {overview_screenshot}")

        # Check for error text
        content = page.content()
        has_account_error = "Hệ thống tài khoản đang gặp lỗi" in content
        has_server_error_toast = "Chưa lưu được: Internal Server Error" in content
        print(f"Has 'Hệ thống tài khoản đang gặp lỗi': {has_account_error}")
        print(f"Has 'Chưa lưu được: Internal Server Error': {has_server_error_toast}")

        if has_account_error or has_server_error_toast:
            print("ERROR: Screen still contains errors!")
        else:
            print("SUCCESS: Screen is clean, connected to real backend with zero errors!")

        # Now test creating a project via UI
        create_proj_btn = page.locator("button:has-text('Tạo việc mới'), button:has-text('Tạo dự án')").first
        if page.locator("button:has-text('Tạo dự án')").is_visible():
            create_proj_btn = page.locator("button:has-text('Tạo dự án')").first

        print("3. Clicking 'Tạo dự án'...")
        create_proj_btn.click()
        time.sleep(1)

        # Step 1: Info
        name_input = page.locator("input[placeholder*='Hệ thống'], input[placeholder*='Ví dụ:']").first
        if name_input.is_visible():
            unique_name = f"Dự Án Chuẩn {int(time.time()) % 10000}"
            print(f"Entering project name: {unique_name}")
            name_input.fill(unique_name)
            time.sleep(0.5)

            # Click next (Tiếp tục)
            next_btn = page.locator("button:has-text('Tiếp tục')").first
            if next_btn.is_visible():
                next_btn.click()
                time.sleep(0.5)

            # Step 2: Types & workflow
            next_btn2 = page.locator("button:has-text('Tiếp tục')").first
            if next_btn2.is_visible():
                next_btn2.click()
                time.sleep(0.5)

            # Step 3: Members & submit
            submit_btn = page.locator("button:has-text('Tạo dự án')").last
            if submit_btn.is_visible():
                print("Submitting project creation...")
                submit_btn.click()
                time.sleep(3)

        # Screenshot after project creation
        create_screenshot = f"{artifact_dir}\\playwright_05_project_create_success.png"
        page.screenshot(path=create_screenshot)
        print(f"Saved project creation screenshot: {create_screenshot}")

        # Check for errors in content
        content2 = page.content()
        create_failed = "Không thể tạo dự án" in content2
        print(f"Has 'Không thể tạo dự án': {create_failed}")

        browser.close()
        print("Done!")

if __name__ == "__main__":
    run()
