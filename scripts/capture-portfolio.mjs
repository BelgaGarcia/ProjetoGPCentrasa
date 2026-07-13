import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { existsSync, mkdirSync, rmSync } from 'node:fs';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, '..');
const webRoot = join(repositoryRoot, 'src', 'CentraSA.Web');
const outputDirectory = join(repositoryRoot, 'docs', 'screenshots');
const temporaryRoot = join(tmpdir(), `CentraSA-Portfolio-${randomUUID()}`);
const storageRoot = join(temporaryRoot, 'storage');
const browserProfilesRoot = join(temporaryRoot, 'browsers');
const chromeCandidates = [
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
];
const browserPath = chromeCandidates.find(existsSync);

class CookieJar {
  constructor() {
    this.cookies = new Map();
  }

  async fetch(url, options = {}) {
    const headers = new Headers(options.headers ?? {});
    if (this.cookies.size > 0) {
      headers.set(
        'Cookie',
        [...this.cookies.entries()].map(([name, value]) => `${name}=${value}`).join('; '),
      );
    }
    const response = await fetch(url, { ...options, headers });
    for (const setCookie of response.headers.getSetCookie()) {
      const [pair] = setCookie.split(';', 1);
      const separator = pair.indexOf('=');
      this.cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
    }
    return response;
  }
}

if (!browserPath) {
  throw new Error('Google Chrome ou Microsoft Edge não foi encontrado.');
}

const applicationDll = join(webRoot, 'bin', 'Release', 'net8.0', 'CentraSA.Web.dll');
if (!existsSync(applicationDll)) {
  throw new Error('Compile a solução em Release antes de gerar os screenshots.');
}

mkdirSync(outputDirectory, { recursive: true });
mkdirSync(browserProfilesRoot, { recursive: true });

const applicationPort = await reservePort();
const baseUrl = `http://127.0.0.1:${applicationPort}`;
const captureToken = randomUUID().replaceAll('-', '');
const password = `portfolio-${randomUUID().replaceAll('-', '')}-1a`;
let application;
let applicationLog = '';

try {
  application = spawn(
    'dotnet',
    ['bin/Release/net8.0/CentraSA.Web.dll', '--urls', baseUrl],
    {
      cwd: webRoot,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        DOTNET_ENVIRONMENT: 'Development',
        HttpsRedirection__Enabled: 'false',
        Logging__LogLevel__Default: 'Warning',
        PortfolioCapture__Token: captureToken,
        SeedDemoData: 'true',
        Storage__DataDirectory: storageRoot,
      },
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    },
  );
  application.stdout.on('data', chunk => { applicationLog += chunk.toString(); });
  application.stderr.on('data', chunk => { applicationLog += chunk.toString(); });

  await waitForHttp(`${baseUrl}/conta/configuracao-inicial`, 60_000);
  process.stdout.write('Aplicação temporária pronta.\n');

  const cookies = new CookieJar();
  const setupResponse = await cookies.fetch(`${baseUrl}/conta/configuracao-inicial`);
  const setupHtml = await setupResponse.text();
  const antiforgeryToken = extractAntiforgeryToken(setupHtml);
  const setupBody = new URLSearchParams({
    UserName: 'portfolio-demo',
    Password: password,
    ConfirmPassword: password,
    __RequestVerificationToken: antiforgeryToken,
  });
  const creationResponse = await cookies.fetch(
    `${baseUrl}/conta/configuracao-inicial`,
    {
      method: 'POST',
      body: setupBody,
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      redirect: 'manual',
    },
  );
  if (creationResponse.status !== 302) {
    throw new Error(`Criação do administrador retornou HTTP ${creationResponse.status}.`);
  }
  process.stdout.write('Administrador temporário criado.\n');

  const captures = [
    ['dashboard-desktop.png', '/', 1440, 960],
    ['pending-tasks.png', '/pendencias', 1440, 1200],
    ['smuds-board.png', '/smuds/apresentacao', 1920, 1080],
    ['ticket-relations.png', '/chamados', 1440, 1200],
    ['meeting-builder.png', '/reunioes/nova', 1440, 1080],
    ['history-mobile.png', '/historico', 500, 844],
  ];

  for (const [fileName, route, width, height] of captures) {
    const loginUrl = new URL('/portfolio/capture-login', baseUrl);
    loginUrl.searchParams.set('token', captureToken);
    loginUrl.searchParams.set('path', route);
    const screenshotPath = join(outputDirectory, fileName);
    const profilePath = join(browserProfilesRoot, fileName.replace('.png', ''));

    await runBrowserCapture(loginUrl.toString(), screenshotPath, profilePath, width, height);
    process.stdout.write(`Capturado: ${fileName}\n`);
  }
}
catch (error) {
  process.stderr.write(`\nFalha na captura:\n${error.stack ?? error}\n`);
  if (applicationLog) {
    process.stderr.write(`\nLog da aplicação:\n${applicationLog}\n`);
  }
  throw error;
}
finally {
  if (application?.exitCode === null) {
    application.kill();
  }
  await delay(1_000);
  try {
    safeRemoveTemporaryRoot(temporaryRoot);
  }
  catch (cleanupError) {
    process.stderr.write(`Aviso de limpeza temporária: ${cleanupError.message}\n`);
  }
}

async function reservePort() {
  return new Promise((resolvePort, reject) => {
    const server = createServer();
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      server.close(() => resolvePort(address.port));
    });
  });
}

async function waitForHttp(url, timeoutMilliseconds) {
  const deadline = Date.now() + timeoutMilliseconds;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url, { redirect: 'manual' });
      if (response.status < 500) {
        return;
      }
    }
    catch {
      // A aplicação ainda está inicializando.
    }
    await delay(250);
  }
  throw new Error(`A aplicação não respondeu em ${url}.`);
}

async function runBrowserCapture(url, screenshotPath, profilePath, width, height) {
  return new Promise((resolveCapture, reject) => {
    const browser = spawn(
      browserPath,
      [
        '--headless=new',
        '--hide-scrollbars',
        '--no-first-run',
        '--no-default-browser-check',
        '--run-all-compositor-stages-before-draw',
        '--virtual-time-budget=3000',
        `--user-data-dir=${profilePath}`,
        `--window-size=${width},${height}`,
        `--screenshot=${screenshotPath}`,
        url,
      ],
      { stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true },
    );
    let output = '';
    browser.stdout.on('data', chunk => { output += chunk.toString(); });
    browser.stderr.on('data', chunk => { output += chunk.toString(); });
    browser.on('error', reject);
    browser.on('exit', code => {
      if (code === 0 && existsSync(screenshotPath)) {
        resolveCapture();
      }
      else {
        reject(new Error(`O navegador encerrou com código ${code}. ${output}`));
      }
    });
  });
}

function extractAntiforgeryToken(html) {
  const match = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
  if (!match) {
    throw new Error('Token antiforgery não encontrado na configuração inicial.');
  }
  return decodeHtml(match[1]);
}

function decodeHtml(value) {
  return value
    .replaceAll('&quot;', '"')
    .replaceAll('&#x2B;', '+')
    .replaceAll('&#x2F;', '/')
    .replaceAll('&#x3D;', '=')
    .replaceAll('&amp;', '&');
}

function delay(milliseconds) {
  return new Promise(resolveDelay => setTimeout(resolveDelay, milliseconds));
}

function safeRemoveTemporaryRoot(directory) {
  const resolvedDirectory = resolve(directory);
  const resolvedTemp = `${resolve(tmpdir())}${sep}`;
  if (!resolvedDirectory.startsWith(resolvedTemp)
      || !basename(resolvedDirectory).startsWith('CentraSA-Portfolio-')) {
    throw new Error(`Recusa ao remover diretório temporário inesperado: ${resolvedDirectory}`);
  }
  rmSync(resolvedDirectory, {
    recursive: true,
    force: true,
    maxRetries: 5,
    retryDelay: 200,
  });
}
