const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'] });
  try {
    const page = await browser.newPage();
    await page.goto('http://localhost:4200/auth', { waitUntil: 'networkidle2' });
    await page.type('input[type="email"]', 'hoanghaihau989@gmail.com');
    await page.type('input[type="password"]', '@123456xX');
    await page.click('button[type="submit"]');
    await page.waitForNavigation({ waitUntil: 'networkidle2' });

    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 1500));

    // Mở inline create
    await page.evaluate(() => {
      document.querySelector('.backlog-pool button.button-primary')?.click();
    });
    await new Promise(r => setTimeout(r, 800));

    const info = await page.evaluate(() => {
      const select = document.querySelector('.type-select');
      if (!select) return 'NOT_FOUND';
      return {
        optionsCount: select.options.length,
        option0: select.options[0] ? { text: select.options[0].text, val: select.options[0].value, selected: select.options[0].selected } : null,
        value: select.value,
        selectedIndex: select.selectedIndex
      };
    });
    console.log('DEBUG SELECT:', JSON.stringify(info, null, 2));
  } catch (err) {
    console.error(err);
  } finally {
    await browser.close();
  }
})();
