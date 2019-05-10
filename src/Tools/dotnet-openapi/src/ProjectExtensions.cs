// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Microsoft.DotNet.OpenApi
{
    public static class ProjectExtensions
    {
        public static ProjectItemGroupElement FindUniformOrCreateItemGroupWithCondition(this ProjectRootElement root, string projectItemElementType, string framework)
        {
            var lastMatchingItemGroup = FindExistingUniformItemGroupWithCondition(root, projectItemElementType, framework);

            if (lastMatchingItemGroup != null)
            {
                return lastMatchingItemGroup;
            }

            ProjectItemGroupElement ret = root.CreateItemGroupElement();
            if (TryGetFrameworkConditionString(framework, out string condStr))
            {
                ret.Condition = condStr;
            }

            root.InsertAfterChild(ret, root.LastItemGroup());
            return ret;
        }

        public static void AddElementWithAttributes(this Project project, string tagName, string include, IDictionary<string, string> metadata)
        {
            var root = ProjectRootElement.Open(project.FullPath);
            var element = root.CreateItemElement(tagName, include);
            var itemGroup = root.FindUniformOrCreateItemGroupWithCondition(tagName, framework: null);
            itemGroup.AppendChild(element);
            foreach (var kvp in metadata)
            {
                element.AddMetadata(kvp.Key, kvp.Value, expressAsAttribute: true);
            }

            project.Save();
        }

        public static ProjectItemGroupElement LastItemGroup(this ProjectRootElement root)
        {
            return root.ItemGroupsReversed.FirstOrDefault();
        }

        public static ProjectItemGroupElement FindExistingUniformItemGroupWithCondition(this ProjectRootElement root, string projectItemElementType, string framework)
        {
            return root.ItemGroupsReversed.FirstOrDefault((itemGroup) => itemGroup.IsConditionalOnFramework(framework) && itemGroup.IsUniformItemElementType(projectItemElementType));
        }

        public static bool IsConditionalOnFramework(this ProjectElement el, string framework)
        {
            if (!TryGetFrameworkConditionString(framework, out string conditionStr))
            {
                return el.ConditionChain().Count == 0;
            }

            var condChain = el.ConditionChain();
            return condChain.Count == 1 && condChain.First().Trim() == conditionStr;
        }

        private static bool TryGetFrameworkConditionString(string framework, out string condition)
        {
            if (string.IsNullOrEmpty(framework))
            {
                condition = null;
                return false;
            }

            condition = $"'$(TargetFramework)' == '{framework}'";
            return true;
        }

        public static bool IsUniformItemElementType(this ProjectItemGroupElement group, string projectItemElementType)
        {
            return group.Items.All((it) => it.ItemType == projectItemElementType);
        }

        public static ISet<string> ConditionChain(this ProjectElement projectElement)
        {
            var conditionChainSet = new HashSet<string>();

            if (!string.IsNullOrEmpty(projectElement.Condition))
            {
                conditionChainSet.Add(projectElement.Condition);
            }

            foreach (var parent in projectElement.AllParents)
            {
                if (!string.IsNullOrEmpty(parent.Condition))
                {
                    conditionChainSet.Add(parent.Condition);
                }
            }

            return conditionChainSet;
        }
    }
}
