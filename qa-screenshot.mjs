// 사용법: node qa-screenshot.mjs http://localhost:3000 / /login /dashboard
// 첫 번째 값은 주소, 나머지는 검사할 화면 경로(없으면 "/"만 검사)
import { chromium } from 'playwright';
import fs from 'node:fs';

const [base, ...paths] = process.argv.slice(2);
if (!base) {
  console.error('주소가 필요합니다. 예) node qa-screenshot.mjs http://localhost:3000 / /login');
  process.exit(1);
}
const pages = paths.length ? paths : ['/'];

// 폰 / 태블릿 / PC 3가지 화면 폭
const sizes = [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'pc', width: 1440, height: 900 },
];

const outDir = 'qa/screens';
fs.mkdirSync(outDir, { recursive: true });

const report = [];
const browser = await chromium.launch();

for (const p of pages) {
  for (const s of sizes) {
    const ctx = await browser.newContext({ viewport: { width: s.width, height: s.height } });
    const page = await ctx.newPage();

    const consoleErrors = [];
    page.on('console', (m) => m.type() === 'error' && consoleErrors.push(m.text()));
    page.on('pageerror', (e) => consoleErrors.push(String(e)));

    const slug = (p === '/' ? 'home' : p.replace(/[^a-z0-9]+/gi, '_').replace(/^_|_$/g, ''));
    const file = `${outDir}/${slug}_${s.name}.png`;
    const item = { page: p, size: `${s.name} ${s.width}x${s.height}`, screenshot: file, problems: [] };

    try {
      await page.goto(base + p, { waitUntil: 'networkidle', timeout: 30000 });
      await page.screenshot({ path: file, fullPage: true });

      // 1) 가로 스크롤이 생기는지 (화면 밖으로 삐져나감)
      const overflow = await page.evaluate(() => ({
        scrollW: document.documentElement.scrollWidth,
        clientW: document.documentElement.clientWidth,
      }));
      if (overflow.scrollW > overflow.clientW + 1) {
        item.problems.push(`가로 넘침: 내용 폭 ${overflow.scrollW} > 화면 폭 ${overflow.clientW}`);
      }

      // 2) 화면 오른쪽 밖으로 나간 요소, 글자가 잘린 요소 찾기
      const bad = await page.evaluate(() => {
        const out = [];
        const vw = document.documentElement.clientWidth;
        for (const el of document.querySelectorAll('body *')) {
          const r = el.getBoundingClientRect();
          const st = getComputedStyle(el);
          if (r.width === 0 || r.height === 0 || st.visibility === 'hidden' || st.display === 'none') continue;
          const name = el.tagName.toLowerCase() + (el.id ? '#' + el.id : '') +
            (el.className && typeof el.className === 'string' ? '.' + el.className.trim().split(/\s+/)[0] : '');
          if (r.right > vw + 1) out.push(`${name}: 오른쪽으로 ${Math.round(r.right - vw)}px 나감`);
          if (el.scrollWidth > el.clientWidth + 1 && st.overflow !== 'visible' && el.children.length === 0 && el.textContent.trim()) {
            out.push(`${name}: 글자가 잘림`);
          }
        }
        return out.slice(0, 15);
      });
      item.problems.push(...bad);

      // 3) 이미지가 깨졌는지
      const brokenImgs = await page.evaluate(() =>
        [...document.images].filter((i) => !i.complete || i.naturalWidth === 0).map((i) => i.src).slice(0, 10)
      );
      brokenImgs.forEach((src) => item.problems.push(`이미지 깨짐: ${src}`));

      // 4) 콘솔 에러
      consoleErrors.slice(0, 10).forEach((e) => item.problems.push(`콘솔 에러: ${e}`));
    } catch (e) {
      item.problems.push(`화면을 열지 못함: ${e.message}`);
    }

    item.result = item.problems.length ? '문제 있음' : '통과';
    report.push(item);
    await ctx.close();
  }
}

await browser.close();

fs.writeFileSync('qa/report.json', JSON.stringify(report, null, 2));

// 한눈에 보는 표
const lines = ['| 화면 | 크기 | 결과 | 문제 수 | 스크린샷 |', '|---|---|---|---|---|'];
for (const r of report) {
  lines.push(`| ${r.page} | ${r.size} | ${r.result} | ${r.problems.length} | ${r.screenshot} |`);
}
fs.writeFileSync('qa/report.md', lines.join('\n') + '\n');
console.log(lines.join('\n'));
console.log('\n자세한 내용: qa/report.json');
