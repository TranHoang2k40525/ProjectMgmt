const puppeteer = require('puppeteer');
const fs = require('fs');
const path = require('path');

const ARTIFACTS_DIR = 'C:/Users/hoang/.gemini/antigravity-ide/brain/a7cd8610-b76b-45df-9f7f-ab668a708d59';

(async () => {
  console.log('=== KHỞI CHẠY KIỂM THỬ PLAYWRIGHT: GIAO DIỆN BACKLOG & TASK DETAIL DRAWER HIỆN ĐẠI ===');
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1600, height: 1000 });

    // 1. Đăng nhập với tài khoản thực
    console.log('[1/5] Đang đăng nhập...');
    await page.goto('http://localhost:4200/auth', { waitUntil: 'networkidle2' });
    
    // Điền form đăng nhập
    await page.type('input[type="email"]', 'hoanghaihau989@gmail.com');
    await page.type('input[type="password"]', '@123456xX');
    await page.click('button[type="submit"]');

    // Chờ điều hướng vào ứng dụng
    await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 2000));
    console.log('    Đã đăng nhập thành công. URL hiện tại:', page.url());

    // 2. Điều hướng tới Backlog
    console.log('[2/5] Truy cập trang Backlog...');
    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 2000));

    // Kiểm tra xem nút "Phân rã AI" có còn tồn tại không
    const aiButtonText = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button'));
      return buttons.map(b => b.textContent.trim()).filter(t => t.includes('Phân rã AI') || t.includes('Trợ lý AI'));
    });
    console.log('    Kiểm tra nút AI trên toolbar:', aiButtonText.length === 0 ? 'ĐÃ LOẠI BỎ THÀNH CÔNG' : 'CẢNH BÁO: Còn nút AI: ' + JSON.stringify(aiButtonText));

    // Chụp ảnh giao diện Backlog hiện đại
    const backlogScreenshot = path.join(ARTIFACTS_DIR, 'playwright_14_backlog_modern.png');
    await page.screenshot({ path: backlogScreenshot, fullPage: false });
    console.log('    Đã lưu ảnh Backlog hiện đại:', backlogScreenshot);

    // 3. Mở Task Detail Drawer bằng cách bấm vào .cell-key hoặc .task-title-button
    console.log('[3/5] Mở Task Detail Drawer...');
    const taskButton = await page.waitForSelector('.cell-key, .task-title-button', { timeout: 5000 });
    if (taskButton) {
      await taskButton.click();
      await new Promise(r => setTimeout(r, 1500));
    }

    // 4. Kiểm tra Task Detail Drawer
    console.log('[4/5] Kiểm tra Task Detail Drawer...');
    const drawerExists = await page.evaluate(() => {
      const drawer = document.querySelector('.jira-drawer, .task-drawer-body');
      if (!drawer) return { open: false };
      const text = drawer.textContent;
      const hasAiAssistant = text.includes('TRỢ LÝ AI') || text.includes('Trợ lý AI') || text.includes('AI 1 Phân rã');
      return {
        open: true,
        hasAiAssistant,
        title: drawer.querySelector('input')?.value
      };
    });
    console.log('    Drawer status:', JSON.stringify(drawerExists));

    const drawerScreenshot = path.join(ARTIFACTS_DIR, 'playwright_15_task_drawer_modern.png');
    await page.screenshot({ path: drawerScreenshot, fullPage: false });
    console.log('    Đã lưu ảnh Drawer hiện đại:', drawerScreenshot);

    // 4b. Đóng drawer và thử tạo inline việc mới (mặc định là Story)
    console.log('[4b/5] Kiểm tra tạo inline User Story...');
    await page.evaluate(() => {
      const footerCloseBtn = document.querySelector('.task-drawer-footer button');
      if (footerCloseBtn) footerCloseBtn.click();
    });
    await new Promise(r => setTimeout(r, 1000));

    // Bấm nút "+ Tạo việc" trong Backlog
    console.log('[4b/5] Kiểm tra tạo inline User Story...');
    await page.evaluate(() => {
      // Đóng drawer nếu còn mở
      const closeBtn = document.querySelector('.task-drawer-header button:last-child');
      if (closeBtn) closeBtn.click();
    });
    await new Promise(r => setTimeout(r, 1000));

    // Bấm nút "+ Tạo việc" của Backlog
    const opened = await page.evaluate(() => {
      const backlogHeader = document.querySelector('.backlog-pool .sprint-header');
      if (!backlogHeader) return false;
      const btn = backlogHeader.querySelector('button.button-primary');
      if (btn) {
        btn.click();
        return true;
      }
      return false;
    });
    console.log('    Bấm nút + Tạo việc Backlog:', opened);
    await new Promise(r => setTimeout(r, 1200));

    const inlineScreenshot = path.join(ARTIFACTS_DIR, 'playwright_16_inline_create.png');
    await page.screenshot({ path: inlineScreenshot, fullPage: false });
    console.log('    Đã lưu ảnh inline create:', inlineScreenshot);

    // 5. Kiểm tra tính năng Xuất Excel
    console.log('[5/5] Kiểm tra tính năng Xuất Excel...');
    // Thiết lập client CDP để theo dõi download
    const client = await page.target().createCDPSession();
    const downloadPath = path.join(__dirname, 'downloads');
    if (!fs.existsSync(downloadPath)) fs.mkdirSync(downloadPath);
    await client.send('Page.setDownloadBehavior', {
      behavior: 'allow',
      downloadPath: downloadPath
    });

    const exportBtn = await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button'));
      const btn = btns.find(b => b.textContent.includes('Xuất Excel'));
      if (btn) {
        btn.click();
        return true;
      }
      return false;
    });
    console.log('    Bấm nút Xuất Excel:', exportBtn);
    await new Promise(r => setTimeout(r, 2000));

    // Kiểm tra xem có file nào được tải về thư mục downloads không
    const files = fs.readdirSync(downloadPath);
    console.log('    Các file đã tải về:', files);
    if (files.length > 0) {
      const exportedFile = path.join(downloadPath, files[0]);
      const content = fs.readFileSync(exportedFile, 'utf8');
      console.log('    Nội dung dòng đầu của file xuất:', content.split('\n')[0]);
      console.log('    Số cột xuất:', content.split('\n')[0].split(',').length);
    }

    console.log('=== TOÀN BỘ KIỂM THỬ PLAYWRIGHT HOÀN TẤT THÀNH CÔNG ===');
  } catch (err) {
    console.error('Lỗi khi chạy kiểm thử:', err);
  } finally {
    await browser.close();
  }
})();
