// mRemoteNG test dosyalarını uygulamadan bağımsız (Node.js crypto) üretir: node make.mjs
// Biçim: AES-256-GCM, PBKDF2-HMAC-SHA1 anahtar; base64(salt16 | nonce16 | şifreli | etiket16), ek veri = salt.
// Eski biçim (ConfVersion < 2.6): AES-CBC, anahtar = MD5(parola); base64(iv16 | şifreli).
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const here = path.dirname(new URL(import.meta.url).pathname);

const gcm = (text, password, iterations = 1000) => {
  if (text === '') return '';
  const salt = crypto.randomBytes(16), nonce = crypto.randomBytes(16);
  const key = crypto.pbkdf2Sync(Buffer.from(password, 'utf8'), salt, iterations, 32, 'sha1');
  const c = crypto.createCipheriv('aes-256-gcm', key, nonce);
  c.setAAD(salt);
  const body = Buffer.concat([c.update(text, 'utf8'), c.final()]);
  return Buffer.concat([salt, nonce, body, c.getAuthTag()]).toString('base64');
};

const cbc = (text, password) => {
  if (text === '') return '';
  const iv = crypto.randomBytes(16);
  const key = crypto.createHash('md5').update(password, 'utf8').digest();
  const c = crypto.createCipheriv('aes-128-cbc', key, iv);
  return Buffer.concat([iv, c.update(text, 'utf8'), c.final()]).toString('base64');
};

const esc = s => s.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');

// Gerçek dosyalardaki gibi ilgisiz alanlar da yazılır.
const node = (enc, n) => {
  const a = {
    Name: n.name, Type: n.type ?? 'Connection', Descr: '', Icon: 'mRemoteNG', Panel: 'General', Id: n.id,
    Username: n.user ?? '', Domain: n.domain ?? '', Password: enc(n.password ?? ''), Hostname: n.host ?? '',
    Protocol: n.protocol ?? 'RDP', PuttySession: 'Default Settings', Port: String(n.port ?? 3389),
    ConnectToConsole: 'false', RenderingEngine: 'IE', Resolution: 'FitToWindow', Colors: 'Colors16Bit',
    InheritProtocol: String(n.inheritProtocol ?? false), InheritPort: String(n.inheritPort ?? false),
    InheritUsername: String(n.inheritUser ?? false), InheritPassword: String(n.inheritPassword ?? false),
    InheritDomain: String(n.inheritDomain ?? false), InheritIcon: 'false', InheritPanel: 'false',
  };
  if (n.type === 'Container') a.Expanded = String(n.expanded ?? true);
  const attrs = Object.entries(a).map(([k, v]) => `${k}="${esc(v)}"`).join(' ');
  const children = (n.children ?? []).map(c => node(enc, c)).join('\n');
  return n.children ? `<Node ${attrs}>\n${children}\n</Node>` : `<Node ${attrs} />`;
};

const tree = [
  { type: 'Container', name: 'Üretim', id: '6f1c2a10-0000-4000-8000-000000000001', user: 'admin', domain: 'AVVA', password: 'klasor-sifre', children: [
    { name: 'web-01', id: '6f1c2a10-0000-4000-8000-000000000011', host: '10.0.1.11', port: 3390, inheritUser: true, inheritPassword: true, inheritDomain: true },
    { name: 'web-02', id: '6f1c2a10-0000-4000-8000-000000000012', host: '10.0.1.12', user: 'ali', password: 'ali-ğüş', inheritDomain: true },
    { name: 'linux-01', id: '6f1c2a10-0000-4000-8000-000000000013', host: '10.0.2.5', protocol: 'SSH2', port: 22, user: 'root', password: 'kok', domain: 'YOKSAY' },
    { name: 'vnc-01', id: '6f1c2a10-0000-4000-8000-000000000014', host: '10.0.3.1', protocol: 'VNC', port: 5900 },
  ] },
  { type: 'Container', name: 'Test', id: '6f1c2a10-0000-4000-8000-000000000002', protocol: 'SSH2', port: 2222, expanded: false, children: [
    { name: '', id: 'kimliksiz', host: 'test.local', inheritProtocol: true, inheritPort: true, user: 'deneme' },
  ] },
  { name: 'eski-telnet', id: '6f1c2a10-0000-4000-8000-000000000021', host: '10.0.9.9', protocol: 'Telnet', port: 23 },
];

const file = ({ version = '2.6', password = 'mR3m', fullFile = false, legacy = false, nodes = tree }) => {
  const enc = legacy ? t => cbc(t, password) : t => gcm(t, password);
  const isProtected = password !== 'mR3m';
  const body = nodes.map(n => node(enc, n)).join('\n');
  const protectedValue = enc(isProtected ? 'ThisIsProtected' : 'ThisIsNotProtected');
  const head = legacy
    ? `Name="Connections" Export="false" Protected="${protectedValue}" ConfVersion="${version}"`
    : `Name="Connections" Export="false" EncryptionEngine="AES" BlockCipherMode="GCM" KdfIterations="1000" FullFileEncryption="${fullFile}" Protected="${protectedValue}" ConfVersion="${version}"`;
  const inner = fullFile ? gcm(body, password) : body;
  return `<?xml version="1.0" encoding="utf-8"?>\n<mrng:Connections xmlns:mrng="http://mremoteng.org" ${head}>\n${inner}\n</mrng:Connections>\n`;
};

fs.writeFileSync(path.join(here, 'confCons.xml'), file({}));
fs.writeFileSync(path.join(here, 'confCons-master.xml'), file({ password: 'ana-parola' }));
fs.writeFileSync(path.join(here, 'confCons-fullfile.xml'), file({ fullFile: true }));
fs.writeFileSync(path.join(here, 'confCons-legacy.xml'), file({ version: '2.5', legacy: true }));
fs.writeFileSync(path.join(here, 'not-mremoteng.xml'), '<?xml version="1.0" encoding="utf-8"?>\n<settings><item name="x" /></settings>\n');
