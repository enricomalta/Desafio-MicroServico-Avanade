const fs = require('fs');
const jwt = require('jsonwebtoken');

const privateKey = fs.readFileSync('private.key', 'utf8');
const publicKey = fs.readFileSync('public.key', 'utf8');

const payload = { userId: 123, role: 'admin' };

const token = jwt.sign(payload, privateKey, {
    algorithm: 'RS256',
    expiresIn: '7d',
    audience: 'MeusUsuarios', // igual ao appsettings.json
    issuer: 'MinhaEmpresa'    // igual ao appsettings.json
});

console.log("Token JWT gerado:\n", token);