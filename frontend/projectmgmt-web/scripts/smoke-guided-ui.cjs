const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const puppeteer = require('puppeteer');

const baseUrl = (process.env.GUIDED_BASE_URL || 'http://127.0.0.1:4300').replace(/\/$/, '');
const viewports = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'phone', width: 390, height: 844 },
  { name: 'phone-landscape', width: 844, height: 390 },
  { name: 'ipad-portrait', width: 768, height: 1024 },
  { name: 'ipad-landscape', width: 1024, height: 768 }
];

const exactAssets = {
  'public/landing-pages/inner-green-3d.html': '69c3694bd63f44ef9f007ebe4dac57a83e4402e0cdf6b54dd10b96dd4f05e197',
  'public/landing-pages/inner-green-assets/three.min.js': '8a5f7249903b54d30f79f708699d2fed2d6a1d0741a4cd41377d1f01bb5a2271',
  'public/landing-pages/inner-green-assets/card-ecostove.jpg': '70ce084084902bc502f00c366405b661ecdff90dee95d363b36a6e146829e433',
  'public/landing-pages/inner-green-assets/card-ethos.jpg': '337627390f499b3ae272cec9e2f83c817694a82f42e1aa10a7b26a2c7d679dff',
  'public/landing-pages/inner-green-assets/lexend-latin.woff2': '1ec8f6ee2750554b4bc59ff0b507d316a82a7ba37e0e5bebc41d3bd9b9faad46'
};

function verifySource() {
  for (const [file, expected] of Object.entries(exactAssets)) {
    const actual = createHash('sha256').update(readFileSync(resolve(__dirname, '..', file))).digest('hex');
    assert.equal(actual, expected, `${file} no longer matches the registered ThreeUI source`);
  }
  process.stdout.write('Exact Sylva source: 5 SHA-256 hashes matched\n');
}

async function assertNoOverflow(page, label) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  assert.ok(overflow <= 1, `${label} overflows viewport by ${overflow}px`);
}

async function checkAuth(browser, viewport) {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.setViewport({ width: viewport.width, height: viewport.height, deviceScaleFactor: 1 });
    await page.goto(`${baseUrl}/auth`, { waitUntil: 'networkidle2', timeout: 90000 });
    await page.waitForSelector('iframe');
    await assertNoOverflow(page, `Auth arrival ${viewport.name}`);

    if (viewport.name === 'desktop') {
      const sceneFrame = page.frames().find(frame => frame.url().includes('inner-green-3d.html'));
      assert.ok(sceneFrame, 'Canonical Sylva iframe did not load');
      await sceneFrame.click('.dock-item--enter');
    } else {
      await page.click('.arrival-skip');
    }

    await page.waitForSelector('#loginEmail');
    await page.waitForFunction(() => !document.querySelector('iframe'), { timeout: 5000 });
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'loginEmail', `Auth focus ${viewport.name}`);
    await assertNoOverflow(page, `Auth panel ${viewport.name}`);
    assert.deepEqual(errors, [], `Auth script errors ${viewport.name}`);
    process.stdout.write(`Auth ${viewport.name}: Enter/skip, focus and layout passed\n`);
  } finally {
    await page.close();
  }
}

async function checkWorkspace(browser, viewport) {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.setViewport({ width: viewport.width, height: viewport.height, deviceScaleFactor: 1 });
    await page.goto(`${baseUrl}/for-you`, { waitUntil: 'networkidle2', timeout: 90000 });
    await page.waitForSelector('#focus-heading');
    const state = await page.evaluate(() => ({
      heading: document.querySelector('#focus-heading')?.textContent?.trim(),
      cta: !!document.querySelector('.focus-cta'),
      oldScene: !!document.querySelector('app-neo-campus-scene'),
      avatarLoaded: (() => { const image = document.querySelector('a[href="/profile"] img'); return !!image && image.complete && image.naturalWidth > 0; })()
    }));
    assert.ok(state.heading && state.cta, `Next action missing at ${viewport.name}`);
    assert.equal(state.oldScene, false, `Old WebGL scene remained at ${viewport.name}`);
    assert.equal(state.avatarLoaded, true, `Profile avatar was broken at ${viewport.name}`);
    await assertNoOverflow(page, `For You ${viewport.name}`);

    if (viewport.name === 'desktop') {
      await page.click('.focus-cta');
      await page.waitForSelector('app-task-detail-drawer .task-drawer-backdrop', { timeout: 5000 });
    }
    assert.deepEqual(errors, [], `Workspace script errors ${viewport.name}`);
    process.stdout.write(`For You ${viewport.name}: next action, avatar and layout passed\n`);
  } finally {
    await page.close();
  }
}

async function checkReducedMotionLogin(browser) {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.emulateMediaFeatures([{ name: 'prefers-reduced-motion', value: 'reduce' }]);
    await page.setViewport({ width: 390, height: 844 });
    await page.goto(`${baseUrl}/auth`, { waitUntil: 'networkidle2', timeout: 90000 });
    await page.waitForSelector('#loginEmail');
    assert.equal(await page.$('iframe'), null, 'Reduced-motion auth loaded 3D scene');
    await page.click('#loginEmail');
    await page.keyboard.down('Control');
    await page.keyboard.press('A');
    await page.keyboard.up('Control');
    await page.type('#loginEmail', 'dev.nguyen@scrumai.io');
    await page.waitForFunction(() => !document.querySelector('form button[type=submit]')?.disabled, { timeout: 5000 });
    await page.click('form button[type=submit]');
    await page.waitForFunction(() => location.pathname === '/for-you', { timeout: 10000 });
    await page.waitForSelector('#focus-heading');
    const greeting = await page.$eval('h1', element => element.textContent.trim());
    assert.match(greeting, /Nguyễn Văn Dev/);
    await assertNoOverflow(page, 'Reduced-motion developer workspace');
    assert.deepEqual(errors, [], 'Reduced-motion login script errors');
    process.stdout.write('Reduced-motion developer login: direct form and role-aware workspace passed\n');
  } finally {
    await page.close();
  }
}

(async () => {
  verifySource();
  const browser = await puppeteer.launch({ headless: true, timeout: 60000 });
  try {
    for (const viewport of viewports) await checkAuth(browser, viewport);
    for (const viewport of viewports) await checkWorkspace(browser, viewport);
    await checkReducedMotionLogin(browser);
    process.stdout.write('Guided UI smoke test passed\n');
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
