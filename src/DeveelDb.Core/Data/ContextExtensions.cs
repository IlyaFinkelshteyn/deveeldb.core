﻿// 
//  Copyright 2010-2017 Deveel
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Deveel.Data.Configuration;
using Deveel.Data.Services;

namespace Deveel.Data {
	public static class ContextExtensions {
		public static IContext Create(this IContext context, string name) {
			return new Context(context, name);
		}

		public static void RegisterService<TService, TImplementation>(this IContext context, object key = null)
			where TImplementation : class, TService {
			context.Scope.Register<TService, TImplementation>(key);
		}

		public static void RegisterService<TImplementation>(this IContext context, object key = null)
			where TImplementation : class {
			context.Scope.Register<TImplementation>();
		}

		public static void RegisterInstance<TService>(this IContext context, object instance, object key = null) {
			context.Scope.RegisterInstance<TService>(instance);
		}

		public static TService ResolveService<TService>(this IContext context, object key = null) {
			return context.Scope.Resolve<TService>(key);
		}

		public static IEnumerable<TService> ResolveAllServices<TService>(this IContext context) {
			return context.Scope.ResolveAll<TService>();
		}

		public static IEnumerable<IDbObjectManager> GetObjectManagers(this IContext context, DbObjectType objectType) {
			return context.ResolveAllServices<IDbObjectManager>()
				.Where(x => x.ObjectType == objectType);
		}

		public static IEnumerable<IDbObjectManager> GetObjectManagers(this IContext context) {
			return context.ResolveAllServices<IDbObjectManager>();

		}

		public static async Task<IDbObject> GetObjectAsync(this IContext context, DbObjectType objectType,
			ObjectName objectName) {
			var managers = context.GetObjectManagers(objectType);
			foreach (var manager in managers) {
				var obj = await manager.GetObjectAsync(objectName);
				if (obj != null)
					return obj;
			}

			return null;
		}

		public static async Task<DbObjectType> GetObjectType(this IContext context, ObjectName objectName) {
			var managers = context.GetObjectManagers();
			foreach (var manager in managers) {
				if (await manager.ObjectExistsAsync(objectName))
					return manager.ObjectType;
			}

			throw new InvalidOperationException($"Object {objectName} not found in this context");
		}

		public static async Task<bool> ObjectExistsAsync(this IContext context, DbObjectType objectType, ObjectName objectName) {
			var managers = context.GetObjectManagers(objectType);
			foreach (var manager in managers) {
				if (await manager.ObjectExistsAsync(objectName))
					return true;
			}

			return false;
		}

		public static async Task<IDbObjectInfo> GetObjectInfo(this IContext context, DbObjectType objectType, ObjectName objectName) {
			var managers = context.GetObjectManagers(objectType);
			foreach (var manager in managers) {
				var objInfo = await manager.GetObjectInfoAsync(objectName);
				if (objInfo != null)
					return objInfo;
			}

			return null;
		}

		public static ObjectName QualifyName(this IContext context, ObjectName objectName) {
			if (objectName.Parent == null) {
				var currentSchema = context.GetValue<string>("currentSchema", null);
				if (String.IsNullOrWhiteSpace(currentSchema))
					throw new InvalidOperationException("None schema was set in context");

				objectName = new ObjectName(new ObjectName(currentSchema), objectName.Name);
			}

			return objectName;
		}

		public static Task<ObjectName> ResolveNameAsync(this IContext context, ObjectName objectName) {
			var ignoreCase = context.GetValue("ignoreCase", true);

			var name = context.QualifyName(objectName);
			var managers = context.GetObjectManagers();
			foreach (var manager in managers) {
				var resolved = manager.ResolveNameAsync(name, ignoreCase);
				if (resolved != null)
					return resolved;
			}

			return null;
		}
	}
}