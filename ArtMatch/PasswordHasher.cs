using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using ArtMatch.Interfaces;
using Microsoft.AspNetCore.Identity;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);
}
public class PasswordHasher :   IPasswordHasher
{
    // 128-bit salt
    private const int SaltSize = 16;
    // 256-bit hash
    private const int HashSize = 32;
    
    // Parameters for Argon2id (tune these based on your infrastructure performance)
    private const int Iterations = 4;
    private const int MemorySize = 65536; // 64 MB
    private const int Parallelism = 4;

    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = HashPasswordWithArgon2(password, salt);

        // Combine salt and hash for storage: {salt}{hash}
        byte[] combinedBytes = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, combinedBytes, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, combinedBytes, SaltSize, HashSize);

        return Convert.ToBase64String(combinedBytes);
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        try 
        {
            byte[] combinedBytes = Convert.FromBase64String(storedHash);
            
            // Extract salt
            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(combinedBytes, 0, salt, 0, SaltSize);

            // Extract stored hash
            byte[] storedHashBytes = new byte[HashSize];
            Buffer.BlockCopy(combinedBytes, SaltSize, storedHashBytes, 0, HashSize);

            // Compute hash of input password using extracted salt
            byte[] computedHash = HashPasswordWithArgon2(password, salt);

            // Constant-time comparison to prevent timing attacks
            return CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHash);
        }
        catch
        {
            return false;
        }
    }

    private byte[] HashPasswordWithArgon2(string password, byte[] salt)
    {
        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = Parallelism;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;

            return argon2.GetBytes(HashSize);
        }
    }
}