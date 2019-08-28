using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Misaka.Interfaces
{
    public interface IDBContext
    {
        string ConnectionString
        {
            get;
            set;
        }
        
    }
}
