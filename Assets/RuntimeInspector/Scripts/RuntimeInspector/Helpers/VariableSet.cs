using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RuntimeInspectorNamespace
{
    [Serializable]
    public class VariableSet
    {
        private const string INCLUDE_ALL_VARIABLES = "*";

#pragma warning disable 0649
        [SerializeField]
        private string m_type;
        public string type { get => m_type; set => m_type = value; }

        [SerializeField]
        private string[] m_variables;
        public string[] variables { get => m_variables; set => m_variables = value; }
#pragma warning restore 0649

        public bool Init()
        {
            var type = RuntimeInspectorUtils.GetType(m_type);
            if (type == null)
                return false;

            variables = new string[m_variables.Length];
            for (int i = 0; i < m_variables.Length; i++)
            {
                if (m_variables[i] != INCLUDE_ALL_VARIABLES)
                    variables[i] = m_variables[i];
                else
                {
                    AddAllVariablesToSet(type);
                    break;
                }
            }

            return true;
        }

        private void AddAllVariablesToSet(Type type)
        {
            MemberInfo[] variables = type.GetAllVariables();
            if (variables != null)
            {
                for (int i = 0; i < variables.Length; i++)
                    this.variables[i] = variables[i].Name;
            }
        }
    }
}