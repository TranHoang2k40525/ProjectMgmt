const { chromium } = require('C:/Users/hoang/AppData/Roaming/npm/node_modules/@playwright/cli/node_modules/playwright-core');
const path = require('path');

const ARTIFACT_DIR = 'C:/Users/hoang/.gemini/antigravity-ide/brain/a7cd8610-b76b-45df-9f7f-ab668a708d59';

async function main() {
  console.log('1. Launching browser...');
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  page.on('console', msg => {
    if (msg.type() === 'error') console.log(`[CONSOLE ERROR]`, msg.text());
  });

  page.on('response', resp => {
    if (resp.url().includes('/api/') && resp.status() >= 400) {
      console.log(`[API ERROR ${resp.status()}] ${resp.url()}`);
    }
  });

  console.log('2. Logging in...');
  await page.goto('http://localhost:4200/auth', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1000);
  await page.fill('#loginEmail', 'hoanghaihau989@gmail.com');
  await page.fill('#loginPass', '@123456xX');
  await page.click('button:has-text("Đăng nhập")');
  await page.waitForURL(url => !url.href.includes('/auth'), { timeout: 10000 });
  await page.waitForTimeout(2000);

  console.log('3. Navigating to Test Project Auto -> Spec Hub...');
  await page.locator('text="Test Project Auto"').first().click();
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000);

  const specTab = page.locator('button:has-text("Đặc tả (Spec)"), a:has-text("Đặc tả (Spec)")').first();
  await specTab.click();
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000);

  console.log('4. Creating a new txt document...');
  // Click on "Tạo tệp .txt"
  const createFileBtn = page.locator('button:has-text("Tạo tệp .txt"), button:has-text("Tệp .txt")').first();
  if (await createFileBtn.isVisible()) {
    await createFileBtn.click();
    await page.waitForTimeout(1000);

    // Look for modal or prompt
    const modalInput = page.locator('input[placeholder*="tên"], input[placeholder*="Name"], input[type="text"]').last();
    if (await modalInput.isVisible()) {
      console.log('Entering file name...');
      await modalInput.fill('dacta-hethong');
      await page.waitForTimeout(500);

      const confirmBtn = page.locator('button:has-text("Tạo"), button:has-text("Xác nhận")').last();
      await confirmBtn.click();
      await page.waitForTimeout(3000);
    }
  }

  const screenshotFileCreated = path.join(ARTIFACT_DIR, 'playwright_13_spec_file_created.png');
  await page.screenshot({ path: screenshotFileCreated });
  console.log('Saved screenshot:', screenshotFileCreated);

  await browser.close();
  console.log('Done creation test!');
}

main().catch(err => {
  console.error('Test error:', err);
  process.exit(1);
});
