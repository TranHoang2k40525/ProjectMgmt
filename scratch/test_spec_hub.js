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

  console.log('3. Clicking on Test Project Auto...');
  await page.locator('text="Test Project Auto"').first().click();
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000);

  console.log('4. Clicking on "Đặc tả (Spec)" tab...');
  const specTab = page.locator('button:has-text("Đặc tả (Spec)"), a:has-text("Đặc tả (Spec)")').first();
  await specTab.click();
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(3000);

  console.log('Current URL in Spec Hub:', page.url());
  const screenshotSpecHub = path.join(ARTIFACT_DIR, 'playwright_12_spec_hub.png');
  await page.screenshot({ path: screenshotSpecHub });
  console.log('Saved Spec Hub screenshot:', screenshotSpecHub);

  // Check if creating a new file or folder works
  console.log('5. Testing New File/Folder action...');
  const newFolderBtn = page.locator('button:has-text("Thư mục mới"), button[title*="Thư mục"]').first();
  if (await newFolderBtn.isVisible()) {
    console.log('New folder button found!');
  }

  await browser.close();
  console.log('Done Spec Hub test!');
}

main().catch(err => {
  console.error('Spec Hub test failed:', err);
  process.exit(1);
});
