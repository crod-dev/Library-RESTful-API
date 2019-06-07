using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.API.Models
{
    /// <summary>
    /// 09 This class supports HATEOAS to entire collections of resources rather than each individual resource
    /// </summary>
    /// <typeparam name="T"></typeparam>
    // 09 Inherits LinkedResourceBaseDto abstract class to inherit the collection of links property
    // 09 Constraint: the type of class this class should work on should also be or inherit from LinkedResourceBaseDto
    // 09 so that it also contains a collection of links property
    public class LinkedCollectionResourceWrapperDto<T>: LinkedResourceBaseDto where T: LinkedResourceBaseDto
    {
        public IEnumerable<T> Value { get; set; }

        public LinkedCollectionResourceWrapperDto(IEnumerable<T> value)
        {
            Value = value;
        }
    }
}
