using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Character.Configuration
{
    internal class CharacterServerConfigurationModel
    {
        public string Address { get; set; }
        public string PublicAdress { get; set; }
        public int Port { get; set; }
        public string Backlog { get; set; }
        public bool UseHash { get; set; }
        public bool AllowRegisterOnLogin { get; set; }

    }
}
