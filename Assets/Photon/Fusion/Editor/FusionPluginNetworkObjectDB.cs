// ReSharper disable once CheckNamespace
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
namespace Fusion.Plugin {
  using System.Collections.Generic;
  using System;
  using System.Diagnostics;

  [Serializable]
  // ReSharper disable once InconsistentNaming
  partial class PluginNetworkObjectDB {
    [Serializable]
    public partial class ObjectDataBase {
      public int Id;
      public string TypeName;
    }

    [Serializable]
    [DebuggerDisplay("{TypeName}")]
    public partial class NetworkBehaviourData : ObjectDataBase {
      public int WordCount;
      public string Data;
    }

    [Serializable]
    [DebuggerDisplay("{Name}")]
    public partial class NetworkObjectData : ObjectDataBase {
      public string                     Name;
      public List<NetworkBehaviourData> NetworkedBehaviours = new List<NetworkBehaviourData>();
      public NetworkObjectFlags         Flags;
    }

    [Serializable]
    [DebuggerDisplay("{Name}")]
    public partial class PrefabNetworkObjectData : NetworkObjectData {
      public int ParentId;
    }

    [Serializable]
    [DebuggerDisplay("{UnityAssetGuid}")]
    public partial class PrefabData : NetworkObjectData {
      public string                        UnityAssetGuid;
      public long                          UnityFileId;
      public List<PrefabNetworkObjectData> NestedObjects = new List<PrefabNetworkObjectData>();
    }

    [Serializable]
    [DebuggerDisplay("{ScenePath}")]
    public partial class SceneData {
      public string SceneRef;
      public string ScenePath;
      public List<NetworkObjectData> Objects = new List<NetworkObjectData>();
    }

    [Serializable]
    [DebuggerDisplay("{UnityAssetGuid}-{UnityFileId}-{Name}")]
    public partial class ScriptableObjectData {
      public string UnityAssetGuid;
      public long   UnityFileId;
      public string TypeName;
      public string Name;
      public string Data;
    }
    
    public List<ScriptableObjectData> ScriptableObjects = new List<ScriptableObjectData>();
    public List<PrefabData>           Prefabs           = new List<PrefabData>();
    public List<SceneData>            Scenes            = new List<SceneData>();
  }
}