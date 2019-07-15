/*
 * Copyright ©  2017-2019 Tånneryd IT AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Reflection;

namespace Tanneryd.BulkOperations.EF6.Model
{
    internal class BulkPropertyInfo
    {
        public virtual Type Type { get; set; }
        public virtual string Name { get; set; }
        public PropertyInfo PropertyInfo { get; set; }
    }

    internal class RegularBulkPropertyInfo : BulkPropertyInfo
    {
        public override Type Type => PropertyInfo.PropertyType;
        public override string Name => PropertyInfo.Name;
    }

    internal class ExpandoBulkPropertyInfo : BulkPropertyInfo
    {
    }

    //internal class PropInfo
    //{
    //    public Type Type { get; set; }
    //    public string Name { get; set; }
    //}
}