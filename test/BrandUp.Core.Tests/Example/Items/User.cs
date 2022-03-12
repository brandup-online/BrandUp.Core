using BrandUp.Items;
using System;

namespace BrandUp.Example.Items
{
    public class User : IItem<Guid>
    {
        public Guid Id { get; set; }
        public string Phone { get; set; }
    }
}