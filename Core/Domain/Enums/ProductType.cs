using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums
{
    public enum ProductType
    {
        // Moyka (WASH_BOX)
        Water = 1,
        Foam = 2,
        Wax = 3,

        // Yoqilg'i (FUEL_DISPENSER)
        Petrol = 10,
        Diesel = 11,
        Methane = 12,
        Propane = 13,

        // Elektr (CHARGER)
        Electricity = 20,

        // Suv avtomati (WATER_DISPENSER)
        PurifiedWater = 30,
        ColdWater = 31,
        HotWater = 32,

        // Changyutgich (VACUUM_CLEANER)
        VacuumService = 40,

        // Vending (VENDING_MACHINE)
        Coffee = 50,
        Tea = 51,
        ColdDrink = 52,
        Snack = 53,
    }
}
