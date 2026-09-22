const puppeteer = require('puppeteer');

const delay = (ms) => new Promise(r => setTimeout(r, ms));

(async () => {
  console.log('🚀 Launching Edge browser for deep Mobile & iPad Responsive UX testing...');
  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    headless: true,
    defaultViewport: null
  });

  const page = await browser.newPage();

  try {
    // -------------------------------------------------------------
    // TEST CASE 1: MOBILE PHONE VIEWPORT (390 x 844 - iPhone 14)
    // -------------------------------------------------------------
    console.log('\n--- 📱 TEST 1: Mobile Phone (390 x 844) ---');
    await page.setViewport({ width: 390, height: 844, isMobile: true, hasTouch: true });
    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2' });
    await delay(1000);

    // 1.1 Mobile Search Toggle Test
    console.log('Testing Mobile Search bar toggle...');
    const mobileSearchBtn = await page.$('button[title="Tìm kiếm nhanh"]');
    if (mobileSearchBtn) {
      await mobileSearchBtn.click();
      await delay(500);
      await page.type('input[placeholder="Tìm tên công việc, mã SCRUMAI..."]', 'SCRUMAI');
      await delay(500);
    }

    // Screenshot Mobile Search
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_mobile_search.png' });

    // 1.2 Mobile Sidebar Drawer Test
    console.log('Testing Mobile Sidebar Drawer Toggle...');
    const menuBtn = await page.$('button[title*="Sidebar Menu"]');
    if (menuBtn) {
      await menuBtn.click();
      await delay(500);
    }
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_mobile_sidebar.png' });

    // Close sidebar
    if (menuBtn) {
      await menuBtn.click();
      await delay(500);
    }

    // 1.3 Backlog Page on Mobile
    console.log('Testing Backlog Page on Mobile...');
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_mobile_backlog.png' });

    // 1.4 Mobile Scrum Kanban Board
    console.log('Testing Scrum Kanban Board on Mobile...');
    await page.goto('http://localhost:4200/board', { waitUntil: 'networkidle2' });
    await delay(1000);
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_mobile_board.png' });

    // -------------------------------------------------------------
    // TEST CASE 2: TABLET / IPAD VIEWPORT (768 x 1024 - iPad Mini)
    // -------------------------------------------------------------
    console.log('\n--- 📱 TEST 2: iPad / Tablet (768 x 1024) ---');
    await page.setViewport({ width: 768, height: 1024, isMobile: true, hasTouch: true });
    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2' });
    await delay(1000);
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_ipad_backlog.png' });

    await page.goto('http://localhost:4200/board', { waitUntil: 'networkidle2' });
    await delay(1000);
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_ipad_board.png' });

    await page.goto('http://localhost:4200/projects/proj-001/settings/workflow', { waitUntil: 'networkidle2' });
    await delay(1000);
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_ipad_workflow_settings.png' });

    // -------------------------------------------------------------
    // TEST CASE 3: DESKTOP VIEWPORT (1440 x 900)
    // -------------------------------------------------------------
    console.log('\n--- 🖥️ TEST 3: Desktop (1440 x 900) ---');
    await page.setViewport({ width: 1440, height: 900 });
    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2' });
    await delay(1000);
    await page.screenshot({ path: 'C:/Users/hoang/.gemini/antigravity/brain/89b0efb9-4947-4929-87b6-eadb839672a9/responsive_desktop_backlog.png' });

    console.log('\n✅ ALL RESPONSIVE UX TESTS EXECUTED SUCCESSFULLY!');
  } catch (err) {
    console.error('❌ Error during responsive UX test:', err);
  } finally {
    await browser.close();
  }
})();
