using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RideSharing
{
    public interface IUserEntity
    {
        Task SetLocation(int? newLocation);

        Task<UserEntity> GetState();

        Task SetRide(RideInfo rideInfo);

        void ClearRide(Guid rideId);
    }
}
