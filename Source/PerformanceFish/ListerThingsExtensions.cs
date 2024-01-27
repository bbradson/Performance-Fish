// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;
using PerformanceFish.Listers;

namespace PerformanceFish;

[PublicAPI]
public static class ListerThingsExtensions
{
	public static void RemoveFromGroup(this ListerThings listerThings, Thing thing, ThingRequestGroup group)
		=> ThingsPrepatches.RemoveFromGroupList(listerThings, thing, group);
	
	public static void AddToGroup(this ListerThings listerThings, Thing thing, ThingRequestGroup group)
		=> ThingsPrepatches.AddToGroupList(listerThings, thing, group);
	
	
	public static void RemoveFromDefList(this ListerThings listerThings, Thing thing)
		=> ThingsPrepatches.RemoveFromDefList(listerThings, thing);
	
	public static void AddToDefList(this ListerThings listerThings, Thing thing)
		=> ThingsPrepatches.AddToDefList(listerThings, thing);
}