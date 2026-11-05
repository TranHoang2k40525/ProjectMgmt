const { chromium } = require('C:/Users/hoang/AppData/Roaming/npm/node_modules/@playwright/cli/node_modules/playwright-core');
const path = require('path');

const ARTIFACT_DIR = 'C:/Users/hoang/.gemini/antigravity-ide/brain/a7cd8610-b76b-45df-9f7f-ab668a708d59';

async function main() {
  console.log('1. Launching browser...');
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  page.on('console', msg => {
    console.log(`[CONSOLE ${msg.type()}]`, msg.text());
  });

  page.on('request', req => {
    if (req.url().includes('/api/')) {
      console.log(`[REQ] ${req.method()} ${req.url()}`);
    }
  });

  page.on('requestfailed', req => {
    console.log(`[REQ FAILED] ${req.method()} ${req.url()} - ${req.failure()?.errorText}`);
  });

  page.on('response', resp => {
    if (resp.url().includes('/api/')) {
      console.log(`[RESP] ${resp.status()} ${resp.url()}`);
    }
  });

  console.log('2. Navigating to http://localhost:4200/...');
  await page.goto('http://localhost:4200/', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);
  console.log('Current URL:', page.url());

  if (page.url().includes('/auth') || await page.locator('#loginEmail').isVisible()) {
    console.log('3. Filling login form...');
    await page.fill('#loginEmail', 'hoanghaihau989@gmail.com');
    await page.fill('#loginPass', '@123456xX');
    await page.waitForTimeout(500);
    
    console.log('Clicking login button...');
    await page.click('button:has-text("Đăng nhập")');
    
    console.log('Waiting for URL change from /auth...');
    await page.waitForURL(url => !url.href.includes('/auth'), { timeout: 10000 }).catch(err => {
      console.log('waitForURL error:', err.message);
    });
    await page.waitForTimeout(3000);
  }

  console.log('Current URL after login attempt:', page.url());

  const screenshot1 = path.join(ARTIFACT_DIR, 'playwright_04_org_overview_success.png');
  await page.screenshot({ path: screenshot1 });
  console.log('Saved screenshot 1:', screenshot1);

  await browser.close();
  console.log('Done test run!');
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
