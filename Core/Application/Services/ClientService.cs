using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class ClientService : IClientService
    {
        private readonly IClientRepository _repo;

        public ClientService(IClientRepository repo)
            => _repo = repo;

        public async Task<GenericDto<ClientResultDto>> CreateAsync(CreateClientDto dto)
        {
            var existing = await _repo.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (existing is not null)
                return GenericDto<ClientResultDto>.Error(409, "Bu telefon raqam bilan mijoz allaqachon mavjud.");

            var client = new ClientEntity
            {
                PhoneNumber = dto.PhoneNumber,
                Inn = dto.Inn,
                BankAccount = dto.BankAccount,
                CompanyName = dto.CompanyName,
                IsActive = true
            };

            var created = await _repo.CreateAsync(client);

            return GenericDto<ClientResultDto>.Success(new ClientResultDto
            {
                Id = created.Id,
                ResultMessage = "Mijoz muvaffaqiyatli qo'shildi."
            });
        }

        public async Task<GenericDto<List<ClientItemDto>>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return GenericDto<List<ClientItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<ClientItemDto>> GetByIdAsync(long id)
        {
            var client = await _repo.GetByIdAsync(id);
            if (client is null)
                return GenericDto<ClientItemDto>.Error(404, "Mijoz topilmadi.");

            return GenericDto<ClientItemDto>.Success(ToItem(client));
        }

        public async Task<GenericDto<ClientResultDto>> UpdateAsync(long id, UpdateClientDto dto)
        {
            var client = await _repo.GetByIdAsync(id);
            if (client is null)
                return GenericDto<ClientResultDto>.Error(404, "Mijoz topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) client.PhoneNumber = dto.PhoneNumber;
            if (!string.IsNullOrWhiteSpace(dto.BankAccount)) client.BankAccount = dto.BankAccount;
            if (!string.IsNullOrWhiteSpace(dto.CompanyName)) client.CompanyName = dto.CompanyName;

            await _repo.UpdateAsync(client);

            return GenericDto<ClientResultDto>.Success(new ClientResultDto
            {
                Id = client.Id,
                ResultMessage = "Mijoz ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<ClientResultDto>> DeleteAsync(long id)
        {
            var client = await _repo.GetByIdAsync(id);
            if (client is null)
                return GenericDto<ClientResultDto>.Error(404, "Mijoz topilmadi.");

            await _repo.DeleteAsync(id);

            return GenericDto<ClientResultDto>.Success(new ClientResultDto
            {
                Id = id,
                ResultMessage = "Mijoz o'chirildi."
            });
        }

        private static ClientItemDto ToItem(ClientEntity c) => new()
        {
            Id = c.Id,
            PhoneNumber = c.PhoneNumber,
            Inn = c.Inn,
            BankAccount = c.BankAccount,
            CompanyName = c.CompanyName,
            IsActive = c.IsActive,
            CreatedDate = c.CreatedDate
        };
    }
}
