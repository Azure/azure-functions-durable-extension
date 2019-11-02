using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Chirper.Service
{
    public interface IUserFollows
    {
        void Add(string user);

        void Remove(string user);

        Task<List<string>> Get();
    }
}
