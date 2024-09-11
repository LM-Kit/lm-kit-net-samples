using ChatPlayground.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.Services;

public interface ILLMFileManager
{
    ObservableCollection<ModelInfo> UserModels { get; }
    ObservableCollection<Uri> UnsortedModels { get; }
    bool FileCollectingInProgress { get; }

    event EventHandler? FileCollectingCompleted;

    void Initialize();
    void DeleteModel(ModelInfo modelInfo);
}
