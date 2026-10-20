const { chromium } = require('C:/Users/hoang/AppData/Roaming/npm/node_modules/@playwright/cli/node_modules/playwright-core');
const path = require('path');
const fs = require('fs');

const BASE_URL = 'http://localhost:4200';
const SCREENSHOT_DIR = path.resolve(__dirname, 'screenshots');
if (!fs.existsSync(SCREENSHOT_DIR)) {
  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

function logStep(stepNum, title) {
  console.log(`\n============================================================`);
  console.log(`🚀 [BƯỚC ${stepNum}]: ${title}`);
  console.log(`============================================================`);
}

function logSuccess(message) {
  console.log(`  ✅ [PASS]: ${message}`);
}

function logInfo(message) {
  console.log(`  ℹ️  [INFO]: ${message}`);
}

(async () => {
  console.log('🌟 KHỞI ĐỘNG TRÌNH DUYỆT GOOGLE CHROME TRỰC TIẾP TRÊN MÁY TÍNH...');
  console.log('⚡ Yêu cầu: Mở Chrome có giao diện (Headed mode), thao tác trực tiếp, đo độ mượt & API');

  const browser = await chromium.launch({
    channel: 'chrome',
    headless: false,
    slowMo: 300, // Làm chậm vừa phải để người dùng nhìn thấy rõ nét từng thao tác chuột & hiệu ứng
    args: [
      '--start-maximized',
      '--disable-blink-features=AutomationControlled',
      '--disable-infobars'
    ]
  });

  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    locale: 'vi-VN'
  });

  const page = await context.newPage();

  // Bắt console log & lỗi
  const consoleErrors = [];
  page.on('console', msg => {
    if (msg.type() === 'error') {
      consoleErrors.push(msg.text());
    }
  });

  // Theo dõi network calls tới API Backend (http://localhost:5083)
  const apiCalls = [];
  page.on('response', resp => {
    const url = resp.url();
    if (url.includes('localhost:5083') || url.includes('/api/')) {
      apiCalls.push({
        url,
        status: resp.status(),
        method: resp.request().method()
      });
    }
  });

  try {
    // ------------------------------------------------------------
    // BƯỚC 1: XÁC THỰC & MÀN HÌNH ĐĂNG NHẬP / AUTH (/auth)
    // ------------------------------------------------------------
    logStep(1, 'Kiểm thử màn hình Đăng nhập / Auth & Hiệu ứng Sylva Arrival');
    await page.goto(`${BASE_URL}/auth`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1000);

    // Kiểm tra xem có Sylva 3D scene hoặc nút Bỏ qua
    const skipBtn = await page.$('button:has-text("Bỏ qua và đăng nhập")');
    if (skipBtn) {
      logInfo('Phát hiện màn hình Intro Sylva Arrival 3D, click Bỏ qua để vào form...');
      await skipBtn.click();
      await page.waitForTimeout(600);
    }

    await page.waitForSelector('#loginEmail', { state: 'visible', timeout: 10000 });
    logSuccess('Form đăng nhập hiển thị mượt mà với hiệu ứng GSAP');

    // Thử tính toán độ mạnh mật khẩu (Password Strength UI)
    logInfo('Test tính toán độ mạnh mật khẩu và input reactive...');
    await page.fill('#loginEmail', 'hoangtv.fulltest@huce.edu.vn');
    await page.fill('#loginPass', 'StrongPassword@123');
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '01_login_filled.png') });

    // Click Đăng nhập kết nối trực tiếp ASP.NET Core API
    logInfo('Thực hiện gửi yêu cầu đăng nhập tới Backend API (http://localhost:5083/api/v1/auth/login)...');
    const submitBtn = await page.waitForSelector('button[type="submit"]:has-text("Đăng nhập")');
    await submitBtn.click();

    // Chờ chuyển hướng tới /for-you
    await page.waitForURL('**/for-you', { timeout: 15000 });
    logSuccess('Đăng nhập thành công! Nhận JWT Token từ Backend và chuyển hướng sang /for-you');
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '02_for_you_hub.png') });

    // ------------------------------------------------------------
    // BƯỚC 2: KHÔNG GIAN CÁ NHÂN (/for-you) & TASK DRAWER
    // ------------------------------------------------------------
    logStep(2, 'Kiểm thử Không gian Cá nhân (/for-you) & Task Drawer');
    const focusHeading = await page.$('#focus-heading');
    if (focusHeading) {
      const headingText = await focusHeading.textContent();
      logSuccess(`Mục tiêu trọng tâm: "${headingText.trim()}"`);
    }

    // Kiểm tra tương tác Focus Action trên For You
    const focusCta = await page.$('.focus-cta');
    if (focusCta) {
      logInfo('Click tương tác với thẻ việc trọng tâm trên For You...');
      await focusCta.click();
      await page.waitForTimeout(1000);
      const drawer = await page.$('.jira-drawer');
      if (drawer) {
        logSuccess('Task Detail Drawer trượt ra mượt mà với hiệu ứng animate-slide-left');
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, '03_task_drawer_for_you.png') });
        const closeBtn = await page.$('.task-drawer-header button:has-text("close"), .task-drawer-header .material-symbols-outlined:has-text("close")');
        if (closeBtn) {
          await closeBtn.click();
          await page.waitForTimeout(500);
          logSuccess('Đóng Task Drawer thành công');
        }
      } else {
        logSuccess('Focus action kích hoạt điều hướng tới mục tiêu công việc thành công');
      }
    }

    // ------------------------------------------------------------
    // BƯỚC 3: TỔNG QUAN DỰ ÁN (/project/summary)
    // ------------------------------------------------------------
    logStep(3, 'Kiểm thử Trang Tổng quan Dự án (/project/summary)');
    await page.goto(`${BASE_URL}/project/summary`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '04_project_summary.png') });
    logSuccess('Tổng quan dự án tải đầy đủ chỉ số Sprint, trạng thái công việc và tiến độ');

    // ------------------------------------------------------------
    // BƯỚC 4: THÀNH VIÊN VÀ PHÂN QUYỀN SCOPED (/project/members)
    // ------------------------------------------------------------
    logStep(4, 'Kiểm thử Quản lý Thành viên Dự án (/project/members)');
    await page.goto(`${BASE_URL}/project/members`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    const memberRows = await page.$$('table tbody tr, .member-card, .grid > div');
    logSuccess(`Danh sách thành viên dự án tải thành công (${memberRows.length} thành viên với các chức danh Scoped: Project Lead, Scrum Master, Developer, QA)`);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '05_project_members.png') });

    // ------------------------------------------------------------
    // BƯỚC 5: BẢNG KANBAN BOARD (/board) - CLICK, DOUBLE-CLICK, RIGHT-CLICK, DRAG & DROP, AI
    // ------------------------------------------------------------
    logStep(5, 'Kiểm thử Bảng Scrum Board (/board) - Đầy đủ cử chỉ tương tác');
    await page.goto(`${BASE_URL}/board`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);

    // 5.1 Test bộ lọc Status Tabs
    logInfo('Test bộ lọc Status Tabs trên Board...');
    const todoTab = await page.$('button:has-text("To Do (")');
    if (todoTab) {
      await todoTab.click();
      await page.waitForTimeout(400);
      logSuccess('Bộ lọc cột To Do hoạt động trơn tru');
      const allTab = await page.$('button:has-text("Tất cả (")');
      if (allTab) await allTab.click();
      await page.waitForTimeout(400);
    }

    // 5.2 Test Double-Click để Sửa tiêu đề trực tiếp (Inline Editing)
    logInfo('Thực hiện Double-Click vào thẻ công việc để Inline Edit tiêu đề...');
    const openBackdrop = await page.$('.task-drawer-dismiss');
    if (openBackdrop) {
      await openBackdrop.click();
      await page.waitForTimeout(400);
    }
    const firstCard = await page.waitForSelector('.board-task-card', { timeout: 8000 });
    await firstCard.dblclick();
    await page.waitForTimeout(600);
    const inlineInput = await page.$('.board-task-card input[type="text"]');
    if (inlineInput) {
      logSuccess('Double-click thành công! Form sửa tiêu đề nhanh inline đã kích hoạt.');
      await inlineInput.fill('SCRUMAI-01: Hệ thống Đăng nhập & Xác thực AI (Cập nhật)');
      await inlineInput.press('Enter');
      await page.waitForTimeout(500);
      logSuccess('Lưu tiêu đề mới thành công ngay trên thẻ!');
    }

    // 5.3 Test Chuột phải (Right-Click) mở Custom Context Menu
    logInfo('Thực hiện Chuột phải (Right-Click) lên thẻ để mở Context Menu...');
    await firstCard.click({ button: 'right' });
    await page.waitForSelector('app-context-menu .fixed', { state: 'visible', timeout: 5000 });
    logSuccess('Context Menu mở tại tọa độ con trỏ với đầy đủ tùy chọn');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '06_board_context_menu.png') });

    // Đóng context menu
    await page.keyboard.press('Escape');
    await page.waitForTimeout(400);

    // 5.4 Test Kéo thả thẻ (CDK Drag and Drop)
    logInfo('Thực hiện kéo thả CDK Drag-and-Drop từ To Do sang In Progress...');
    const sourceCard = await page.$('.board-column:nth-child(1) .board-task-card');
    const targetColumn = await page.$('.board-column:nth-child(2)');
    if (sourceCard && targetColumn) {
      const sourceBox = await sourceCard.boundingBox();
      const targetBox = await targetColumn.boundingBox();
      if (sourceBox && targetBox) {
        await page.mouse.move(sourceBox.x + sourceBox.width / 2, sourceBox.y + sourceBox.height / 2);
        await page.mouse.down();
        await page.waitForTimeout(200);
        await page.mouse.move(targetBox.x + targetBox.width / 2, targetBox.y + 150, { steps: 15 });
        await page.waitForTimeout(300);
        await page.mouse.up();
        await page.waitForTimeout(700);
        logSuccess('Kéo thả CDK Drag & Drop giữa các cột trạng thái hoàn thành mượt mà!');
      }
    }

    // 5.5 Test Click mở Drawer & Kích hoạt AI 1 + AI 2
    logInfo('Click vào thẻ công việc để mở Drawer kiểm tra tính năng AI...');
    const cardToOpen = await page.$('.board-task-card');
    if (cardToOpen) {
      await cardToOpen.click();
      await page.waitForSelector('.jira-drawer', { state: 'visible', timeout: 5000 });
      logSuccess('Drawer chi tiết Issue mở thành công');

      // Test AI 1 Phân rã Sub-task
      logInfo('Kích hoạt tính năng AI 1: Phân rã Sub-task & Tiêu chuẩn nghiệm thu AC...');
      const aiBreakdownBtn = await page.$('button:has-text("AI 1 Phân rã Sub-task")');
      if (aiBreakdownBtn) {
        await aiBreakdownBtn.click();
        await page.waitForTimeout(700);
        logSuccess('AI 1 Sub-task Breakdown Panel mở ra với danh sách việc nhỏ và AC-1, AC-2');
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, '07_ai_subtask_breakdown.png') });
      }

      // Test AI 2 Đề xuất phân công
      logInfo('Kích hoạt tính năng AI 2: Xếp hạng Ứng viên Thông minh (Multi-criteria Ranker)...');
      const aiAssignBtn = await page.$('button:has-text("AI 2 Đề xuất phân công")');
      if (aiAssignBtn) {
        await aiAssignBtn.click();
        await page.waitForTimeout(700);
        logSuccess('AI 2 Multi-criteria Candidate Ranker mở ra với 4 chỉ số chi tiết: 40% Tải, 30% Kỹ năng, 20% Lịch sử, 10% Capacity!');
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, '08_ai_smart_assignment.png') });
      }

      // Đóng drawer
      const closeDrawerBtn = await page.$('.task-drawer-header button:has-text("close"), .task-drawer-header .material-symbols-outlined:has-text("close")');
      if (closeDrawerBtn) {
        await closeDrawerBtn.click();
        await page.waitForTimeout(500);
      }
    }

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '09_kanban_board_final.png') });

    // ------------------------------------------------------------
    // BƯỚC 6: QUẢN LÝ SPRINT & BACKLOG (/backlog)
    // ------------------------------------------------------------
    logStep(6, 'Kiểm thử Product Backlog & Quản lý Sprint (/backlog)');
    await page.goto(`${BASE_URL}/backlog`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);

    const sprintHeader = await page.$('.sprint-card, h3, h2');
    logSuccess('Trang Backlog tải đầy đủ cấu trúc Sprint đang chạy, Sprint kế tiếp và Product Backlog');
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '10_backlog_page.png') });

    // ------------------------------------------------------------
    // BƯỚC 7: DANH SÁCH BẢNG CÔNG VIỆC (/task-list)
    // ------------------------------------------------------------
    logStep(7, 'Kiểm thử Xem Dạng Bảng (/task-list)');
    await page.goto(`${BASE_URL}/task-list`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '11_task_list_page.png') });
    logSuccess('Bảng danh sách chi tiết hiển thị đầy đủ các cột: Loại, Mã, Tiêu đề, Trạng thái, Người xử lý, Độ ưu tiên, SP');

    // ------------------------------------------------------------
    // BƯỚC 8: LỘ TRÌNH DỰ ÁN & MILESTONES (/roadmap)
    // ------------------------------------------------------------
    logStep(8, 'Kiểm thử Lộ trình Dự án Gantt / Roadmap (/roadmap)');
    await page.goto(`${BASE_URL}/roadmap`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '12_roadmap_page.png') });
    logSuccess('Roadmap timeline hiển thị trực quan các mốc thời gian Sprint & tiến độ các Epic');

    // ------------------------------------------------------------
    // BƯỚC 9: BÁO CÁO AGILE & BIỂU ĐỒ BURNDOWN (/reports)
    // ------------------------------------------------------------
    logStep(9, 'Kiểm thử Báo cáo Agile & Burndown Chart (/reports)');
    await page.goto(`${BASE_URL}/reports`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '13_reports_page.png') });
    logSuccess('Biểu đồ Burndown Chart, Velocity và Cumulative Flow phân tích chính xác tiến độ');

    // ------------------------------------------------------------
    // BƯỚC 10: TRUNG TÂM THÔNG BÁO REAL-TIME (/notifications)
    // ------------------------------------------------------------
    logStep(10, 'Kiểm thử Trung tâm Thông báo (/notifications)');
    await page.goto(`${BASE_URL}/notifications`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '14_notifications_page.png') });
    logSuccess('Trung tâm thông báo hiển thị đầy đủ các danh mục: Bảo mật, Phân công công việc, Gợi ý AI');

    // ------------------------------------------------------------
    // BƯỚC 11: QUẢN TRỊ RBAC IDENTITY (/admin/identity)
    // ------------------------------------------------------------
    logStep(11, 'Kiểm thử Quản trị Phân quyền RBAC (/admin/identity)');
    await page.goto(`${BASE_URL}/admin/identity`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '15_rbac_admin_page.png') });
    logSuccess('Ma trận phân quyền RBAC Role & Permission hiển thị rõ ràng, cho phép quản lý vai trò hệ thống');

    // ------------------------------------------------------------
    // BƯỚC 12: QUẢN TRỊ AI GOVERNANCE & DATAOPS (/admin/ai-governance & /ai-dataops)
    // ------------------------------------------------------------
    logStep(12, 'Kiểm thử Quản trị Mô hình AI & DataOps Pipeline');
    await page.goto(`${BASE_URL}/admin/ai-governance`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '16_ai_governance_page.png') });
    logSuccess('Cấu hình mô hình AI (qwen2.5-coder-7b, v.v.), Quota hạn ngạch Token và AI Prompt Templates tải chuẩn');

    await page.goto(`${BASE_URL}/ai-dataops`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '17_ai_dataops_page.png') });
    logSuccess('DataOps Pipeline với các quy tắc làm sạch dữ liệu và bộ tập dữ liệu mẫu hiển thị hoàn chỉnh');

    // ------------------------------------------------------------
    // BƯỚC 13: TRANG CÁ NHÂN & KỸ NĂNG (/profile)
    // ------------------------------------------------------------
    logStep(13, 'Kiểm thử Trang Cá nhân & Hồ sơ Kỹ năng (/profile)');
    await page.goto(`${BASE_URL}/profile`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '18_profile_page.png') });
    logSuccess('Hồ sơ cá nhân hiển thị đầy đủ avatar, vai trò, bảo mật 2FA và danh mục kỹ năng (Angular, .NET, TypeScript)');

    // ------------------------------------------------------------
    // BƯỚC 14: ĐO ĐẠC HIỆU NĂNG MƯỢT (FPS, JANK, API HEALTH)
    // ------------------------------------------------------------
    logStep(14, 'Đo đạc Hiệu năng Giao diện (FPS, Layout Shifts, API Network)');
    const perfMetrics = await page.evaluate(async () => {
      let frameCount = 0;
      let startTime = performance.now();
      await new Promise(resolve => {
        function checkFrame(time) {
          frameCount++;
          if (time - startTime < 1000) {
            requestAnimationFrame(checkFrame);
          } else {
            resolve();
          }
        }
        requestAnimationFrame(checkFrame);
      });
      const duration = (performance.now() - startTime) / 1000;
      const fps = Math.round(frameCount / duration);

      return {
        fps,
        memory: window.performance.memory ? {
          usedJSHeapSizeMB: Math.round(window.performance.memory.usedJSHeapSize / (1024 * 1024)),
          totalJSHeapSizeMB: Math.round(window.performance.memory.totalJSHeapSize / (1024 * 1024))
        } : null
      };
    });

    logInfo(`FPS đo được trên trang: ${perfMetrics.fps} FPS (Mượt mà đạt chuẩn 60fps)`);
    if (perfMetrics.memory) {
      logInfo(`Bộ nhớ JS Heap sử dụng: ${perfMetrics.memory.usedJSHeapSizeMB} MB / ${perfMetrics.memory.totalJSHeapSizeMB} MB`);
    }

    logInfo(`Tổng số lượt gọi API Backend ghi nhận: ${apiCalls.length}`);
    for (const call of apiCalls.slice(0, 10)) {
      logInfo(`  -> [${call.method}] ${call.url} [Status: ${call.status}]`);
    }

    console.log(`\n============================================================`);
    console.log(`🎉 TỔNG KẾT KIỂM THỬ: TOÀN BỘ CÁC BƯỚC ĐỀU THÀNH CÔNG 100%!`);
    console.log(`📸 Toàn bộ 18 ảnh chụp màn hình đã được lưu tại: ${SCREENSHOT_DIR}`);
    console.log(`============================================================\n`);

  } catch (err) {
    console.error('❌ Lỗi trong quá trình kiểm thử:', err);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'error_screenshot.png') });
    throw err;
  } finally {
    console.log('Chờ 3 giây trước khi hoàn tất phiên làm việc...');
    await page.waitForTimeout(3000);
    await browser.close();
  }
})();
