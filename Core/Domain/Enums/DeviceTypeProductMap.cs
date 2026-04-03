namespace Domain.Enums
{
    public static class DeviceTypeProductMap
    {
        private static readonly Dictionary<DeviceType, ProductType[]> _map = new()
        {
            [DeviceType.FUEL_DISPENSER]  = [ProductType.Petrol, ProductType.Diesel, ProductType.Methane, ProductType.Propane],
            [DeviceType.WASH_BOX]        = [ProductType.Water, ProductType.Foam, ProductType.Wax],
            [DeviceType.CHARGER]         = [ProductType.Electricity],
            [DeviceType.WATER_DISPENSER] = [ProductType.PurifiedWater, ProductType.ColdWater, ProductType.HotWater],
            [DeviceType.VACUUM_CLEANER]  = [ProductType.VacuumService],
            [DeviceType.VENDING_MACHINE] = [ProductType.Coffee, ProductType.Tea, ProductType.ColdDrink, ProductType.Snack],
        };

        public static ProductType[] GetAllowed(DeviceType deviceType)
            => _map.TryGetValue(deviceType, out var types) ? types : [];

        public static bool IsAllowed(DeviceType deviceType, ProductType productType)
            => GetAllowed(deviceType).Contains(productType);
    }
}
