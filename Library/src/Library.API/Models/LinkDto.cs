using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.API.Models
{
    /// <summary>
    /// 09 Base class for supprting HATEOAS that will be inherited by the dto (response model classes)
    /// </summary>
    public class LinkDto
    {
        public string Href { get; private set; }
        public string Rel { get; private set; }
        public string Method { get; private set; }

        public LinkDto(string href, string rel, string method )
        {
            Href = href;
            Rel = rel;
            Method = method;
        }
    }
}
