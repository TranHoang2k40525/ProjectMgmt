const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

(async () => {
  console.log('Testing live image loading on http://localhost:4200/auth...');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  const loadedResources = [];
  page.on('response', response => {
    if (response.url().includes('huce')) {
      loadedResources.push({ url: response.url(), status: response.status() });
    }
  });

  await page.goto('http://localhost:4200/auth', { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);

  console.log('Loaded HUCE resources status:');
  console.log(JSON.stringify(loadedResources, null, 2));

  // Check if logo image is rendered and naturalWidth > 0
  const imageStatus = await page.evaluate(() => {
    const imgs = Array.from(document.querySelectorAll('img'));
    return imgs.map(i => ({
      src: i.src,
      naturalWidth: i.naturalWidth,
      naturalHeight: i.naturalHeight,
      complete: i.complete
    }));
  });

  console.log('Image element status on DOM:');
  console.log(JSON.stringify(imageStatus, null, 2));

  const screenshotPath = path.join('C:\\Users\\hoang\\.gemini\\antigravity\\brain\\89b0efb9-4947-4929-87b6-eadb839672a9', 'live_image_verify.png');
  await page.screenshot({ path: screenshotPath, fullPage: true });
  console.log('Saved verification screenshot to live_image_verify.png');

  await browser.close();
})();
