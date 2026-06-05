namespace Domain.Enums
{
    /// <summary>
    /// Rolning aniq turi — guruh (Platform/Customer) va scope (global/scoped) birikmasi.
    /// Rol entity'sidan hisoblanadi (PlatformRole.MerchantId / CustomerRole.OrganizationId null-ligi).
    /// Permission biriktirish qoidalarini (<see cref="Domain.Constants.PermissionScopes"/>) belgilaydi.
    /// </summary>
    public enum RoleKind
    {
        PlatformManage,
        PlatformMerchant,
        CustomerNatural,
        CustomerCorporate
    }
}
