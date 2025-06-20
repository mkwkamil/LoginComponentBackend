using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuizApp.Backend.Data;
using QuizApp.Backend.Models;

namespace QuizApp.Backend.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    
    public AuthService(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }
    
    public async Task<bool> Register(User user, string password)
    {
        CreatePasswordHash(password, out byte[] hash, out byte[] salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.CreatedAt = DateTime.UtcNow;
        user.PublicName = user.Username;
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<string?> Login(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Username == username);
        if (user == null || !VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            return null;

        return _tokenService.CreateToken(user);
    }

    public async Task<bool> Logout(string token)
    {
        try
        {
            var principal = _tokenService.ValidateToken(token);
            if (principal == null)
            {
                return false;
            }

            var blacklistedToken = new BlacklistedToken
            {
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddDays(1)
            };
        
            _context.BlackListedTokens.Add(blacklistedToken);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during logout: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> UserExists(string username)
    {
        return await _context.Users.AnyAsync(x => x.Username == username);
    }
    
    public async Task<User?> GetUserByUsername(string username)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    
    private void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
    {
        using (var hmac = new HMACSHA512())
        {
            salt = hmac.Key;
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
    
    private bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
    {
        using (var hmac = new HMACSHA512(storedSalt))
        {
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return computedHash.SequenceEqual(storedHash);
        }
    }
}