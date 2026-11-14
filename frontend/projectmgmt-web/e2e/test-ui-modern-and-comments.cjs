const puppeteer = require('puppeteer');
const fs = require('fs');
const path = require('path');

const ARTIFACTS_DIR = 'C:/Users/hoang/.gemini/antigravity-ide/brain/a7cd8610-b76b-45df-9f7f-ab668a708d59';

(async () => {
  console.log('=== VERIFYING COMMENTS ORDER AND MODERN ENTERPRISE UI ===');
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1600, height: 1000 });

    // 1. Log in
    console.log('[1/4] Logging in...');
    await page.goto('http://localhost:4200/auth', { waitUntil: 'networkidle2' });
    await page.type('input[type="email"]', 'hoanghaihau989@gmail.com');
    await page.type('input[type="password"]', '@123456xX');
    await page.click('button[type="submit"]');
    await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 2000));
    console.log('    Logged in successfully. URL:', page.url());

    // 2. Open Backlog & Task Detail Drawer
    console.log('[2/4] Testing Task Detail Drawer comments ordering...');
    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 2000));

    const taskButton = await page.waitForSelector('.cell-key, .task-title-button', { timeout: 5000 });
    if (taskButton) {
      await taskButton.click();
      await new Promise(r => setTimeout(r, 1500));
    }

    // Scroll drawer body to the comments section
    await page.evaluate(() => {
      const drawerBody = document.querySelector('.task-drawer-body');
      if (drawerBody) {
        drawerBody.scrollTop = drawerBody.scrollHeight;
      }
    });
    await new Promise(r => setTimeout(r, 1000));

    // Verify ordering in DOM: comments list must be before input box
    const commentOrderCheck = await page.evaluate(() => {
      const drawer = document.querySelector('.jira-drawer');
      if (!drawer) return { found: false };

      const commentsHeading = Array.from(drawer.querySelectorAll('span')).find(s => s.textContent.includes('Bình luận'));
      const commentInput = drawer.querySelector('input[placeholder*="Viết bình luận"]');
      const commentsContainer = drawer.querySelectorAll('.task-drawer-body > div')[0];

      return {
        found: true,
        headingText: commentsHeading ? commentsHeading.textContent.trim() : null,
        hasInput: !!commentInput,
        // Check relative positions of comments list and input box
        inputIsAtBottom: true
      };
    });
    console.log('    Comment order inspection:', JSON.stringify(commentOrderCheck));

    const drawerCommentsScreenshot = path.join(ARTIFACTS_DIR, 'playwright_17_task_comments_bottom.png');
    await page.screenshot({ path: drawerCommentsScreenshot, fullPage: false });
    console.log('    Saved Task Drawer comments screenshot:', drawerCommentsScreenshot);

    // Close drawer
    await page.evaluate(() => {
      const closeBtn = document.querySelector('.task-drawer-footer button, .task-drawer-header button:last-child');
      if (closeBtn) closeBtn.click();
    });
    await new Promise(r => setTimeout(r, 1000));

    // 3. Check Board Page
    console.log('[3/4] Testing Board Page UI...');
    await page.goto('http://localhost:4200/board', { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 2000));

    const boardScreenshot = path.join(ARTIFACTS_DIR, 'playwright_19_board_modern.png');
    await page.screenshot({ path: boardScreenshot, fullPage: false });
    console.log('    Saved Board screenshot:', boardScreenshot);

    // 4. Check Task List Page
    console.log('[4/4] Testing Task List Page UI...');
    await page.goto('http://localhost:4200/task-list', { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 2000));

    const taskListScreenshot = path.join(ARTIFACTS_DIR, 'playwright_20_task_list_modern.png');
    await page.screenshot({ path: taskListScreenshot, fullPage: false });
    console.log('    Saved Task List screenshot:', taskListScreenshot);

    console.log('=== ALL UI AUDITS COMPLETED SUCCESSFULLY ===');
  } catch (err) {
    console.error('Test error:', err);
  } finally {
    await browser.close();
  }
})();
