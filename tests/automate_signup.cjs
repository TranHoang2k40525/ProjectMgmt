const { chromium } = require('C:/Users/hoang/AppData/Roaming/npm/node_modules/@playwright/cli/node_modules/playwright-core');

(async () => {
  console.log('🚀 Đang khởi động Google Chrome có giao diện...');
  let browser;
  try {
    browser = await chromium.launch({
      channel: 'chrome',
      headless: false,
      args: ['--start-maximized']
    });
  } catch (err) {
    console.log('Không mở được chrome channel, thử chromium mặc định...');
    browser = await chromium.launch({
      headless: false,
      args: ['--start-maximized']
    });
  }

  const context = await browser.newContext({
    viewport: null,
    locale: 'vi-VN'
  });

  const page = await context.newPage();
  console.log('🌐 Đang truy cập http://localhost:4200/auth ...');
  await page.goto('http://localhost:4200/auth', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1000);

  console.log('👉 Chuyển sang màn hình Đăng ký mới...');
  const signupBtn = await page.waitForSelector('button:has-text("Đăng ký mới")', { timeout: 10000 });
  await signupBtn.click();
  await page.waitForTimeout(600);

  console.log('✍️ Đang điền thông tin đăng ký:');
  console.log('   - Họ và tên: Hoàng Trần Văn');
  await page.fill('#signupName', 'Hoàng Trần Văn');
  await page.waitForTimeout(300);

  console.log('   - Email: hoanghaihau989@gmail.com');
  await page.fill('#signupEmail', 'hoanghaihau989@gmail.com');
  await page.waitForTimeout(300);

  console.log('   - Số điện thoại: 0987654321');
  await page.fill('#signupPhone', '0987654321');
  await page.waitForTimeout(300);

  console.log('   - Mật khẩu: @123456xX');
  await page.fill('#signupPass', '@123456xX');
  await page.waitForTimeout(600);

  console.log('📩 Bấm nút "Đăng ký tài khoản" để gửi yêu cầu và phát mã OTP...');
  const submitBtn = await page.waitForSelector('button[type="submit"]:has-text("Đăng ký tài khoản")');
  await submitBtn.click();

  console.log('⏳ Đang chờ hệ thống xử lý và chuyển sang màn hình OTP...');
  try {
    const firstOtpInput = await page.waitForSelector('input[maxlength="1"]', { timeout: 15000 });
    console.log('\n============================================================');
    console.log('🎉 THÀNH CÔNG! Đã chuyển sang màn hình Xác thực mã OTP!');
    console.log('📧 Mã OTP đã được gửi về hộp thư: hoanghaihau989@gmail.com');
    console.log('👉 Trình duyệt Chrome đang mở sẵn trên màn hình của bạn.');
    console.log('👉 Con trỏ chuột đã được đặt vào ô nhập OTP đầu tiên.');
    console.log('👉 Bây giờ bạn hãy mở Gmail lấy mã OTP 6 số và tự nhập vào nhé!');
    console.log('============================================================\n');
    await firstOtpInput.focus();
  } catch (e) {
    console.log('Kiểm tra phản hồi trên màn hình:', e.message);
  }

  // Giữ tiến trình chạy để cửa sổ trình duyệt không bị đóng tự động
  await new Promise(() => {});
})();
