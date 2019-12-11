using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Chirper.Service
{
    public interface IUserChirps
    {
        void Add(Chirp chirp);

        void Remove(DateTime timestamp);

        Task<List<Chirp>> Get();
    }
}
