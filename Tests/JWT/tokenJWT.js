const fs = require('fs');
const { generateKeyPairSync } = require('crypto');
const jwt = require('jsonwebtoken');

// 1 Gerar par de chaves RSA
const { publicKey, privateKey } = generateKeyPairSync('rsa', {
  modulusLength: 2048,
  publicKeyEncoding: { type: 'spki', format: 'pem' },
  privateKeyEncoding: { type: 'pkcs8', format: 'pem' }
});

// salvar em arquivos
fs.writeFileSync('private.key', privateKey);
fs.writeFileSync('public.key', publicKey);
console.log("Chaves RSA geradas!");

// 2 Criar token JWT
const payload = { userId: 123, role: 'admin' };
const token = jwt.sign(payload, privateKey, { algorithm: 'RS256', expiresIn: '7d' });
console.log("\nToken JWT gerado:\n", token);

// 3 Verificar token JWT
try {
    const decoded = jwt.verify(token, publicKey);
    console.log("\nToken válido! Payload decodificado:");
    console.log(decoded);
} catch (err) {
    console.log("Token inválido:", err.message);
}
