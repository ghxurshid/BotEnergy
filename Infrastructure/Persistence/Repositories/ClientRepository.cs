using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly AppDbContext _context;

        public ClientRepository(AppDbContext context)
            => _context = context;

        public async Task<ClientEntity?> GetByIdAsync(long id)
            => await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        public async Task<List<ClientEntity>> GetAllAsync()
            => await _context.Clients.Where(c => !c.IsDeleted).OrderBy(c => c.CompanyName).ToListAsync();

        public async Task<ClientEntity?> GetByPhoneNumberAsync(string phoneNumber)
            => await _context.Clients.FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber && !c.IsDeleted);

        public async Task<ClientEntity> CreateAsync(ClientEntity client)
        {
            await _context.Clients.AddAsync(client);
            await _context.SaveChangesAsync();
            return client;
        }

        public async Task<ClientEntity> UpdateAsync(ClientEntity client)
        {
            _context.Clients.Update(client);
            await _context.SaveChangesAsync();
            return client;
        }

        public async Task DeleteAsync(long id)
        {
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
            if (client is null) return;
            client.IsDeleted = true;
            client.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
