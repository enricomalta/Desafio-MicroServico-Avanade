using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Common
{
    /// <summary>
    /// Responsável por gerar e validar tokens JWT usando criptografia assimétrica (RSA).
    /// Espera-se que as chaves em formato PEM estejam nos caminhos fornecidos.
    /// </summary>
    public class JwtHandler
    {
        private readonly string _publicKeyPath;   // Caminho do arquivo PEM contendo a chave pública
        private readonly string _issuer;          // Emissor esperado (Issuer)
        private readonly string _audience;        // Audience esperada

        public JwtHandler(string publicKeyPath, string issuer, string audience)
        {
            _publicKeyPath = publicKeyPath;
            _issuer = issuer;
            _audience = audience;
        }

        /// <summary>
        /// Valida um token JWT retornando ClaimsPrincipal se válido; retorna null se inválido.
        /// </summary>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            // Lê e importa a chave pública para validar a assinatura
            var publicKey = File.ReadAllText(_publicKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey.ToCharArray());

            // Parâmetros de validação — importante validar emissor, audiência, tempo de expiração e assinatura
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,              // Garante que expiração é respeitada
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateIssuerSigningKey = true        // Exige assinatura válida
                // OBS: Poderia-se adicionar ClockSkew para tolerância de relógio (ex: ClockSkew = TimeSpan.FromMinutes(2))
            };

            var handler = new JwtSecurityTokenHandler();
            try
            {
                // Se inválido (assinatura, expiração, etc.), exceção é lançada e capturada
                var principal = handler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch
            {
                // Idealmente logar o motivo. Evita vazar detalhe ao consumidor externo.
                return null;
            }
        }

        /// <summary>
        /// Gera um token JWT (backward compatible) sem roles.
        /// Mantido para não quebrar chamadas existentes. Internamente delega para a sobrecarga com roles.
        /// </summary>
        public string GenerateToken(string userId, string userName, string privateKeyPath, int expireMinutes = 60)
            => GenerateToken(userId, userName, privateKeyPath, null, expireMinutes);

        /// <summary>
        /// Gera um token JWT com suporte a roles (claims de autorização).
        /// Roles serão adicionadas individualmente como ClaimTypes.Role.
        /// </summary>
        public string GenerateToken(string userId, string userName, string privateKeyPath, IEnumerable<string>? roles, int expireMinutes = 60)
        {
            var privateKey = File.ReadAllText(privateKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey.ToCharArray());

            var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.UniqueName, userName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (roles != null)
            {
                foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
                }
            }

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}