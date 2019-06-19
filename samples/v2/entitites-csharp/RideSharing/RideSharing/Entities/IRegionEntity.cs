using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RideSharing
{
    public interface IRegionEntity
    {
        Task<string[]> GetAvailableDrivers();

        Task<string[]> GetAvailableRiders();

        void AddUser(string user);

        void RemoveUser(string user);
    }
}
