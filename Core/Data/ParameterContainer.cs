using UnityEngine;
using System.Collections.Generic;
using System;
using System.Globalization;
using Object = UnityEngine.Object;

namespace SpicyTilemapEditor
{
    [Serializable]
    public class ParameterContainer
    {
        private const string WarningMsgParamNotFound = "Parameter with name {0} not found!";

        public List<Parameter> ParameterList => m_paramList;
        public Parameter this[string name] => FindParam(name);

        [SerializeField] private List<Parameter> m_paramList = new();

        public void AddNewParam(Parameter param, int idx = -1)
        {
            idx = idx >= 0 ? Mathf.Min(idx, m_paramList.Count) : m_paramList.Count;
            string origName = param.name;
            int i = 1;
            while (m_paramList.Exists(x => x.name == param.name))
            {
                param.name = origName + " (" + i + ")";
                i++;
            }

            m_paramList.Insert(idx, param);
        }

        public void RemoveParam(string name) => m_paramList.RemoveAll(x => x.name == name);

        public void RenameParam(string name, string newName)
        {
            int idx = m_paramList.FindIndex(x => x.name == name);
            if (idx < 0)
                return;

            Parameter param = m_paramList[idx];
            RemoveParam(name);
            param.name = newName;
            AddNewParam(param, idx);
        }

        public void RemoveAll() => m_paramList.Clear();

        public void SortByName() => m_paramList.Sort((Parameter a, Parameter b) => a.name.CompareTo(b.name));

        public void SortByType() => m_paramList.Sort((Parameter a, Parameter b) => a.GetParamType().CompareTo(b.GetParamType()));

        public Parameter FindParam(string name) => m_paramList.Find(x => x.name == name);


        public void AddParam(string name, bool value) => AddParam<bool>(name, value);

        public void AddParam(string name, int value) => AddParam<int>(name, value);

        public void AddParam(string name, float value) => AddParam<float>(name, value);

        public void AddParam(string name, string value) => AddParam<string>(name, value);

        public void AddParam(string name, UnityEngine.Object value) => AddParam<UnityEngine.Object>(name, value);

        private void AddParam<T>(string name, T value)
        {
            Parameter param = FindParam(name);
            if (param != null)
                Debug.LogWarning($"Parameter with name {param.name} ({param.GetParamType()}) already exist");
            else
            {
                param = value switch
                {
                    bool => new Parameter(name, (bool)(System.Object)value),
                    int => new Parameter(name, (int)(System.Object)value),
                    float => new Parameter(name, (float)(System.Object)value),
                    string => new Parameter(name, (string)(System.Object)value),
                    Object => new Parameter(name, (UnityEngine.Object)(System.Object)value),
                    _ => param
                };
            }

            if (param != null) 
                m_paramList.Add(param);
        }

        public void SetParam(string name, bool value)
        {
            Parameter param = FindParam(name);
            if (param != null)
                param.SetValue(value);
            else
                AddParam<bool>(name, value);
        }

        public void SetParam(string name, int value)
        {
            Parameter param = FindParam(name);
            if (param != null)
                param.SetValue(value);
            else
                AddParam<int>(name, value);
        }

        public void SetParam(string name, float value)
        {
            Parameter param = FindParam(name);
            if (param != null)
                param.SetValue(value);
            else
                AddParam<float>(name, value);
        }

        public void SetParam(string name, string value)
        {
            Parameter param = FindParam(name);
            if (param != null)
                param.SetValue(value);
            else
                AddParam<string>(name, value);
        }

        public void SetParam(string name, UnityEngine.Object value)
        {
            Parameter param = FindParam(name);
            if (param != null)
                param.SetValue(value);
            else
                AddParam<UnityEngine.Object>(name, value);
        }

        public T GetParam<T>(string name, T defaultValue)
        {
            Parameter param = FindParam(name);
            if (param == null)
                return defaultValue;

            if (typeof(T) == typeof(bool))
                return (T)Convert.ChangeType(param.GetAsBool(), typeof(T));

            if (typeof(T) == typeof(int))
                return (T)Convert.ChangeType(param.GetAsInt(), typeof(T));

            if (typeof(T) == typeof(float))
                return (T)Convert.ChangeType(param.GetAsFloat(), typeof(T));

            if (typeof(T) == typeof(string))
                return (T)Convert.ChangeType(param.GetAsString(), typeof(T));

            if (typeof(T).IsSubclassOf(typeof(UnityEngine.Object)))
                return (T)Convert.ChangeType(param.GetAsObject(), typeof(T));
            
            Debug.LogWarning($"Parameter with name {param.name} ({param.GetParamType()}) is not supported. Use type: bool, int, float, string or UnityEngine.Object");
            return defaultValue;
        }

        public int GetIntParam(string name, int defaultValue = 0)
        {
            Parameter param = FindParam(name);
            return param?.GetAsInt() ?? defaultValue;
        }

        public float GetFloatParam(string name, float defaultValue = 0f)
        {
            Parameter param = FindParam(name);
            return param?.GetAsFloat() ?? defaultValue;
        }

        public string GetStringParam(string name, string defaultValue = "")
        {
            Parameter param = FindParam(name);
            return param?.GetAsString() ?? defaultValue;
        }

        public bool GetBoolParam(string name, bool defaultValue = false)
        {
            Parameter param = FindParam(name);
            return param?.GetAsBool() ?? defaultValue;
        }

        public UnityEngine.Object GetObjectParam(string name, UnityEngine.Object defaultValue = null)
        {
            Parameter param = FindParam(name);
            return param?.GetAsObject() ?? defaultValue;
        }

        public void AddValueToIntParam(string name, int value, bool createIfDoesntExist = true)
        {
            Parameter param = FindParam(name);
            if (param != null)
                param.SetValue(param.GetAsInt() + value);
            else if (createIfDoesntExist) 
                AddParam(name, value);
        }

        public void AddValueToFloatParam(string name, float value, bool createIfDoesntExist = true)
        {
            Parameter param = FindParam(name);
            if (param != null)
            {
                param.SetValue(param.GetAsFloat() + value);
            }
            else if (createIfDoesntExist)
            {
                AddParam(name, value);
            }
        }
    }


    public enum EParameterType
    {
        None,
        Bool,
        Int,
        Float,
        Object,
        String,
    }

    [Serializable]
    public class Parameter
    {
        private const string WarningMsgWrongType = "Parameter {0} of type {1} accessed as {2}";

        public string name;

        [SerializeField] private EParameterType paramType = EParameterType.None;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private string stringValue = string.Empty;
        [SerializeField] private UnityEngine.Object objectValue;

        private Parameter(string name) => this.name = name;

        public Parameter(string name, bool value) : this(name)
        {
            boolValue = value;
            paramType = EParameterType.Bool;
        }

        public Parameter(string name, int value) : this(name)
        {
            intValue = value;
            paramType = EParameterType.Int;
        }

        public Parameter(string name, float value) : this(name)
        {
            floatValue = value;
            paramType = EParameterType.Float;
        }

        public Parameter(string name, string value) : this(name)
        {
            stringValue = value;
            paramType = EParameterType.String;
        }

        public Parameter(string name, UnityEngine.Object value) : this(name)
        {
            objectValue = value;
            paramType = EParameterType.Object;
        }

        public override string ToString()
        {
            return paramType switch
            {
                EParameterType.Bool => boolValue.ToString(),
                EParameterType.Int => intValue.ToString(),
                EParameterType.Float => floatValue.ToString(CultureInfo.InvariantCulture),
                EParameterType.String => stringValue.ToString(),
                EParameterType.Object => objectValue.ToString(),
                _ => "<Not defined>"
            };
        }

        public Parameter Clone()
        {
            return new Parameter(this.name)
            {
                paramType = this.paramType,
                boolValue = this.boolValue,
                intValue = this.intValue,
                floatValue = this.floatValue,
                stringValue = this.stringValue,
                objectValue = this.objectValue,
            };
        }

        public EParameterType GetParamType() => paramType;

        public bool GetAsBool()
        {
            Debug.Assert(paramType == EParameterType.Bool,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Bool));
            return boolValue;
        }

        public int GetAsInt()
        {
            Debug.Assert(paramType == EParameterType.Int,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Int));
            return intValue;
        }

        public float GetAsFloat()
        {
            Debug.Assert(paramType == EParameterType.Float,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Float));
            return floatValue;
        }

        public string GetAsString()
        {
            Debug.Assert(paramType == EParameterType.String,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.String));
            return stringValue;
        }

        public UnityEngine.Object GetAsObject()
        {
            Debug.Assert(paramType == EParameterType.Object,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Object));
            return objectValue;
        }

        public void SetValue(bool value)
        {
            Debug.Assert(paramType == EParameterType.Bool,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Bool));
            boolValue = value;
        }

        public void SetValue(int value)
        {
            Debug.Assert(paramType == EParameterType.Int,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Int));
            intValue = value;
        }

        public void SetValue(float value)
        {
            Debug.Assert(paramType == EParameterType.Float,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Float));
            floatValue = value;
        }

        public void SetValue(string value)
        {
            Debug.Assert(paramType == EParameterType.String,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.String));
            stringValue = value;
        }

        public void SetValue(UnityEngine.Object value)
        {
            Debug.Assert(paramType == EParameterType.Object,
                string.Format(WarningMsgWrongType, name, paramType, EParameterType.Object));
            objectValue = value;
        }
    }
}