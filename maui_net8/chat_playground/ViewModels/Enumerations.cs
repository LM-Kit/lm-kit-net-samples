using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.ViewModels
{
    public enum ModelLoadingState
    {
        Unloaded,
        Loading,
        Loaded
    }

    public enum LmKitTextGenerationStatus
    {
        Undefined,
        Cancelled,
        UnknownError
    }
}
