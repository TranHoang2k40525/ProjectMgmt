const { chromium } = require('C:/Users/hoang/AppData/Roaming/npm/node_modules/@playwright/cli/node_modules/playwright-core');
const path = require('path');

const ARTIFACT_DIR = 'C:/Users/hoang/.gemini/antigravity-ide/brain/a7cd8610-b76b-45df-9f7f-ab668a708d59';

async function main() {
  console.log('1. Launching browser...');
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  const failedRequests = [];
  page.on('response', resp => {
    if (resp.url().includes('/api/') && resp.status() >= 400) {
      console.log(`[API ERROR ${resp.status()}] ${resp.url()}`);
      failedRequests.push({ status: resp.status(), url: resp.url() });
    }
  });

  page.on('console', msg => {
    if (msg.type() === 'error') {
      console.log(`[CONSOLE ERROR]`, msg.text());
    }
  });

  console.log('2. Navigating to login...');
  await page.goto('http://localhost:4200/auth', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1000);
  await page.fill('#loginEmail', 'hoanghaihau989@gmail.com');
  await page.fill('#loginPass', '@123456xX');
  await page.click('button:has-text("Đăng nhập")');
  await page.waitForURL(url => !url.href.includes('/auth'), { timeout: 10000 });
  await page.waitForTimeout(2000);

  // Click on "Test Project Auto" in "Gần đây"
  console.log('3. Clicking on recent project: Test Project Auto...');
  const recentProject = page.locator('text="Test Project Auto"').first();
  if (await recentProject.isVisible()) {
    await recentProject.click();
  } else {
    // If not found directly, click on organization "đư"
    await page.locator('text="đư"').first().click();
    await page.waitForTimeout(2000);
    await page.locator('text="Test Project Auto"').first().click();
  }

  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(3000);
  console.log('Current URL in project:', page.url());

  // Capture Project Overview / Board / Backlog
  const screenshotBoard = path.join(ARTIFACT_DIR, 'playwright_08_board.png');
  await page.screenshot({ path: screenshotBoard });
  console.log('Saved board screenshot:', screenshotBoard);

  // Check Spec Hub
  console.log('4. Navigating to Spec Hub...');
  const specHubNav = page.locator('a[href*="spec-hub"], span:has-text("Spec Hub"), span:has-text("Tài liệu")').first();
  if (await specHubNav.isVisible()) {
    await specHubNav.click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    console.log('Current URL in Spec Hub:', page.url());
    const screenshotSpecHub = path.join(ARTIFACT_DIR, 'playwright_09_spec_hub.png');
    await page.screenshot({ path: screenshotSpecHub });
    console.log('Saved Spec Hub screenshot:', screenshotSpecHub);
  }

  // Check Backlog
  console.log('5. Navigating to Backlog...');
  const backlogNav = page.locator('a[href*="backlog"], span:has-text("Backlog")').first();
  if (await backlogNav.isVisible()) {
    await backlogNav.click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    console.log('Current URL in Backlog:', page.url());
    const screenshotBacklog = path.join(ARTIFACT_DIR, 'playwright_10_backlog.png');
    await page.screenshot({ path: screenshotBacklog });
    console.log('Saved Backlog screenshot:', screenshotBacklog);
  }

  // Check Project Settings (Thành viên & Phân quyền, Quy trình)
  console.log('6. Navigating to Project Settings...');
  const settingsNav = page.locator('a[href*="settings"], span:has-text("Cài đặt dự án"), span:has-text("Cài đặt")').first();
  if (await settingsNav.isVisible()) {
    await settingsNav.click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    console.log('Current URL in Settings:', page.url());
    const screenshotSettings = path.join(ARTIFACT_DIR, 'playwright_11_settings.png');
    await page.screenshot({ path: screenshotSettings });
    console.log('Saved Settings screenshot:', screenshotSettings);
  }

  console.log('--- TEST SUMMARY ---');
  console.log('Total API errors >= 400:', failedRequests.length);
  for (const f of failedRequests) {
    console.log(` -> ${f.status} on ${f.url}`);
  }

  await browser.close();
  console.log('Done!');
}

main().catch(err => {
  console.error('Test run failed:', err);
  process.exit(1);
});
