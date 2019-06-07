using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.API.Models
{
    /// <summary>
    /// 09 This is a base class for supporting HATEOAS linked resources 
    /// </summary>
    // 09 Abstract class to prevent intantition of it, rather should only be used by inheritance
    public abstract class LinkedResourceBaseDto
    {
        public List<LinkDto> Links { get; set; } = new List<LinkDto>();
    }
}
