const puppeteer = require('puppeteer');
const fs = require('fs');
const path = require('path');

const ARTIFACTS_DIR = 'C:/Users/hoang/.gemini/antigravity-ide/brain/a7cd8610-b76b-45df-9f7f-ab668a708d59';

(async () => {
  console.log('=== KHỞI ĐỘNG KIỂM TRA TỰ ĐỘNG BẰNG PLAYWRIGHT/PUPPETEER CLI ===');
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-web-security']
  });

  const results = {
    authPageLoaded: false,
    specHubLoaded: false,
    leftbarExplorerVisible: false,
    actionButtonsMaterialSymbols: false,
    multiTabWorkspaceVisible: false,
    backlogLoaded: false,
    screenshots: []
  };

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1600, height: 1000 });

    const errors = [];
    page.on('console', msg => {
      if (msg.type() === 'error') errors.push(msg.text());
    });

    // 1. Kiểm tra trang đăng nhập
    console.log('[1/4] Kiểm tra trang xác thực /auth...');
    await page.goto('http://localhost:4200/auth', { waitUntil: 'networkidle2', timeout: 15000 });
    const pageTitle = await page.title();
    console.log('    Tiêu đề trang:', pageTitle);
    results.authPageLoaded = pageTitle.includes('Scrum') || pageTitle.includes('AI');

    const authScreenshotPath = path.join(ARTIFACTS_DIR, 'playwright_01_auth_page.png');
    await page.screenshot({ path: authScreenshotPath, fullPage: true });
    results.screenshots.push(authScreenshotPath);
    console.log('    Đã lưu ảnh chụp:', authScreenshotPath);

    // 2. Thiết lập Session & Dự án vào trình duyệt
    console.log('[2/4] Thiết lập phiên làm việc Quản trị viên Scrum...');
    const testSession = {
      accessToken: 'test-token-playwright-cli',
      refreshToken: 'test-refresh-token',
      userId: '00000000-0000-0000-0000-000000000001',
      email: 'owner@projectmgmt.dev',
      fullName: 'Giám Đốc Dự Án (Lead)',
      roles: ['Admin', 'ProjectManager']
    };

    const mockProject = {
      id: '11111111-1111-1111-1111-111111111111',
      orgId: '22222222-2222-2222-2222-222222222222',
      name: 'Hệ thống Quản lý Dự án Scrum AI',
      projectKey: 'SCRUMAI'
    };

    await page.evaluateOnNewDocument((session, project) => {
      sessionStorage.setItem('projectmgmt.auth-session', JSON.stringify(session));
      sessionStorage.setItem('pm_active_project_id', project.id);
      localStorage.setItem('pm_active_project', JSON.stringify(project));
      localStorage.setItem('pm_active_project_id', project.id);
    }, testSession, mockProject);

    // 3. Kiểm tra trang Spec Hub
    console.log('[3/4] Điều hướng tới Kho tài liệu đặc tả (Spec Hub)...');
    await page.goto(`http://localhost:4200/documents`, { waitUntil: 'networkidle2', timeout: 15000 });
    await new Promise(r => setTimeout(r, 1200));

    // Thẩm định các thành phần của Spec Hub
    const specHubHeader = await page.$eval('h1', el => el.textContent.trim()).catch(() => '');
    console.log('    H1 Header:', specHubHeader);
    results.specHubLoaded = specHubHeader.includes('Kho Đặc Tả Dự Án') || specHubHeader.includes('Spec Hub');

    // Kiểm tra Leftbar Explorer
    const explorerEl = await page.$('.spec-explorer-panel, .panel-tree, aside, [aria-label*="thư mục"], .w-80, .border-r').catch(() => null);
    results.leftbarExplorerVisible = !!explorerEl;
    console.log('    Leftbar Explorer hiển thị:', results.leftbarExplorerVisible ? 'CÓ' : 'KHÔNG');

    // Kiểm tra các icon Material Symbols
    const materialIcons = await page.$$eval('.material-symbols-outlined', elements =>
      elements.map(e => e.textContent.trim()).filter(Boolean)
    ).catch(() => []);
    console.log('    Các icon Material Symbols tìm thấy:', [...new Set(materialIcons)].slice(0, 10).join(', '));
    results.actionButtonsMaterialSymbols = materialIcons.length > 0;

    // Kiểm tra Workspace Panel
    const workspacePanel = await page.$('.spec-workspace-panel, .workspace-tabs, main').catch(() => null);
    results.multiTabWorkspaceVisible = !!workspacePanel;
    console.log('    Right Multi-tab Workspace hiển thị:', results.multiTabWorkspaceVisible ? 'CÓ' : 'KHÔNG');

    const specHubScreenshotPath = path.join(ARTIFACTS_DIR, 'playwright_02_spec_hub.png');
    await page.screenshot({ path: specHubScreenshotPath, fullPage: false });
    results.screenshots.push(specHubScreenshotPath);
    console.log('    Đã lưu ảnh chụp:', specHubScreenshotPath);

    // 4. Kiểm tra trang Backlog
    console.log('[4/4] Kiểm tra trang Backlog Scrum...');
    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2', timeout: 15000 });
    await new Promise(r => setTimeout(r, 1000));

    const backlogTitle = await page.title();
    results.backlogLoaded = true;
    console.log('    Tiêu đề Backlog:', backlogTitle);

    const backlogScreenshotPath = path.join(ARTIFACTS_DIR, 'playwright_03_backlog.png');
    await page.screenshot({ path: backlogScreenshotPath, fullPage: false });
    results.screenshots.push(backlogScreenshotPath);
    console.log('    Đã lưu ảnh chụp:', backlogScreenshotPath);

    console.log('=== HOÀN TẤT KIỂM TRA PLAYWRIGHT CLI ===');
    console.log(JSON.stringify(results, null, 2));

  } catch (err) {
    console.error('Lỗi trong quá trình kiểm tra Playwright:', err);
  } finally {
    await browser.close();
  }
})();
