const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

(async () => {
  const stitchDir = path.join(__dirname, 'stitch_screens');
  const outputDir = path.join(__dirname, 'stitch_rendered_pngs');
  if (!fs.existsSync(outputDir)) fs.mkdirSync(outputDir, { recursive: true });

  const htmlFiles = fs.readdirSync(stitchDir).filter(f => f.endsWith('.html'));
  console.log(`Found ${htmlFiles.length} Stitch HTML screen files.`);

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  const allImages = [];
  const screenSummaries = [];

  for (const file of htmlFiles) {
    const filePath = path.join(stitchDir, file);
    const fileUrl = 'file:///' + filePath.replace(/\\/g, '/');
    const screenName = file.replace('.html', '');

    console.log(`Rendering ${file}...`);
    await page.goto(fileUrl, { waitUntil: 'load' });
    await page.waitForTimeout(500);

    const screenshotPath = path.join(outputDir, `${screenName}.png`);
    await page.screenshot({ path: screenshotPath, fullPage: true });

    // Extract title, text snippet, and all images
    const screenInfo = await page.evaluate((sName) => {
      const title = document.title || sName;
      const text = document.body.innerText.replace(/\s+/g, ' ').trim().substring(0, 1000);
      const imgs = Array.from(document.querySelectorAll('img')).map(i => ({
        src: i.src,
        alt: i.alt || '',
        width: i.width,
        height: i.height
      }));
      return { screenName: sName, title, textSnippet: text, imgs };
    }, screenName);

    allImages.push(...screenInfo.imgs);
    screenSummaries.push(screenInfo);
  }

  await browser.close();

  fs.writeFileSync(path.join(outputDir, 'stitch_summary.json'), JSON.stringify({
    totalScreens: htmlFiles.length,
    screenSummaries,
    allImages
  }, null, 2));

  console.log('Rendering complete! Saved all screenshots to stitch_rendered_pngs');
})();
