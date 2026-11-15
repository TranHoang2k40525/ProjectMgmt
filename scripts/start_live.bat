@echo off
chcp 65001 >nul
echo ========================================================
echo   KHỞI ĐỘNG HỆ THỐNG CON THẬT (PROJECTMGMT LIVE HOST)
echo ========================================================
echo.

echo [1/3] Kiểm tra IIS Service (W3SVC)...
net start W3SVC >nul 2>&1
echo   -> IIS Backend đang hoạt động trên Port 80 (http://ProjectMgmt.dev.com)
echo.

echo [2/3] Khởi chạy Frontend SPA Proxy Server trên Port 4300...
start "ProjectMgmt SPA Proxy (Port 4300)" node "C:\Users\hoang\Downloads\Test-ProjectMgmt\serve-spa.cjs"
timeout /t 2 /nobreak >nul
echo   -> SPA Proxy đã chạy tại: http://localhost:4300
echo.

echo [3/3] Khởi chạy Cloudflare Public Tunnel (Internet)...
start "ProjectMgmt Cloudflare Tunnel" cloudflared tunnel --url http://localhost:4300
echo   -> Cloudflare Tunnel đang kết nối ra Internet...
echo.
echo ========================================================
echo   HỆ THỐNG ĐÃ SỐNG HOÀN TOÀN!
echo   - Localhost : http://localhost:4300
echo   - Xem cửa sổ Cloudflare Tunnel để lấy đường link công khai (.trycloudflare.com)
echo ========================================================
pause
