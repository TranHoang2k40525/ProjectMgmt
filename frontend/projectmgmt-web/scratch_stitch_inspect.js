const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

(async () => {
  console.log('Launching browser to inspect Stitch project 3489937127396055955...');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  const url = 'https://stitch.withgoogle.com/projects/3489937127396055955';
  console.log(`Navigating to ${url}...`);
  
  try {
    await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 });
  } catch (err) {
    console.log('Initial navigation timeout or load notice:', err.message);
  }

  // Wait extra 5 seconds for dynamic JS
  await page.waitForTimeout(5000);

  const title = await page.title();
  console.log('Page Title:', title);

  const currentUrl = page.url();
  console.log('Current URL:', currentUrl);

  // Capture screenshot of the Stitch project page
  const outputDir = path.join(__dirname, 'stitch_inspect');
  if (!fs.existsSync(outputDir)) fs.mkdirSync(outputDir, { recursive: true });

  await page.screenshot({ path: path.join(outputDir, 'stitch_page_main.png'), fullPage: true });
  console.log('Saved stitch_page_main.png');

  // Extract all img sources, background images, links, text content
  const pageData = await page.evaluate(() => {
    const images = Array.from(document.querySelectorAll('img')).map(img => ({
      src: img.src,
      alt: img.alt,
      width: img.width,
      height: img.height,
      className: img.className
    }));

    const textContent = document.body.innerText;
    const links = Array.from(document.querySelectorAll('a')).map(a => ({
      href: a.href,
      text: a.innerText
    }));

    const buttons = Array.from(document.querySelectorAll('button')).map(b => b.innerText);

    return {
      images,
      textContent: textContent.substring(0, 5000),
      links,
      buttons
    };
  });

  fs.writeFileSync(path.join(outputDir, 'stitch_page_data.json'), JSON.stringify(pageData, null, 2));
  console.log('Saved stitch_page_data.json');

  await browser.close();
})();
