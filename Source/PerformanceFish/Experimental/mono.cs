// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if false
using System.Runtime.InteropServices;

namespace PerformanceFish.Experimental;

// ReSharper disable once InconsistentNaming
[StaticConstructorOnStartup]
public class JITPatch
{
	static JITPatch()
	{
		
	}
}

/*
 * Handling System.Type objects:
 *
 *   Fields defined as System.Type in managed code should be defined as MonoObject* 
 * in unmanaged structures, and the monotype_cast () function should be used for 
 * casting them to MonoReflectionType* to avoid crashes/security issues when 
 * encountering instances of user defined subclasses of System.Type.
 */

/* This corresponds to System.Type */
[MONO_RT_MANAGED_ATTR]
struct MonoReflectionType
{
	MonoObject @object;
	MonoType* type;
}

/* This corresponds to System.RuntimeType */
unsafe struct MonoReflectionMonoType
{
	MonoReflectionType type;
	MonoObject* type_info;
}

/*
 * The following structure must match the C# implementation in our corlib.
 */

[MONO_RT_MANAGED_ATTR]
unsafe struct MonoReflectionMethod
{
	MonoObject @object;
	MonoMethod* method;
	MonoString* name;
	MonoReflectionType* reftype;
}
/* Safely access System.Reflection.MonoMethod from native code */

[MONO_RT_MANAGED_ATTR]
unsafe struct MonoReflectionMethodBody
{
	MonoObject @object;
	MonoArray* clauses;
	MonoArray* locals;
	MonoArray* il;
	MonoBoolean init_locals;
	uint local_var_sig_token;
	uint max_stack;
}

unsafe struct MonoMethodInfo
{
	MonoReflectionType* parent;
	MonoReflectionType* ret;
	uint attrs;
	uint implattrs;
	uint callconv;
}

[MONO_RT_MANAGED_ATTR]
struct MonoString
{
	MonoObject @object;
	int length;
	ushort chars [MONO_ZERO_LEN_ARRAY];
}

[MONO_RT_MANAGED_ATTR]
unsafe struct MonoArray
{
	MonoObject obj;
	/* bounds is NULL for szarrays */
	MonoArrayBounds* bounds;
	/* total number of elements of the array */
	uint max_length; 
	/* we use double to ensure proper alignment on platforms that need it */
	double vector [MONO_ZERO_LEN_ARRAY];
}

struct MonoArrayBounds
{
	uint length;
	int lower_bound;
}

struct MonoBoolean
{
	byte Value;
}

[MONO_RT_MANAGED_ATTR]
unsafe struct MonoObject
{
	MonoVTable* vtable;
	MonoThreadsSync* synchronisation;
}

unsafe struct MonoMethod {
	ushort flags;  /* method flags */
	ushort iflags; /* method implementation flags */
	uint token;
	MonoClass* klass; /* To what class does this method belong */
	MonoMethodSignature* signature;
	/* name is useful mostly for debugging */
	readonly char* name;
	/* this is used by the inlining algorithm */
	uint inline_info:1;
	uint inline_failure:1;
	uint wrapper_type:5;
	uint string_ctor:1;
	uint save_lmf:1;
	uint dynamic:1; /* created & destroyed during runtime */
	uint sre_method:1; /* created at runtime using Reflection.Emit */
	uint is_generic:1; /* whenever this is a generic method definition */
	uint is_inflated:1; /* whether we're a MonoMethodInflated */
	uint skip_visibility:1; /* whenever to skip JIT visibility checks */
	uint verification_success:1; /* whether this method has been verified successfully.*/
	int slot : 16;

	/*
	 * If is_generic is TRUE, the generic_container is stored in image->property_hash, 
	 * using the key MONO_METHOD_PROP_GENERIC_CONTAINER.
	 */
}

unsafe struct MonoClass
{
	/* element class for arrays and enum basetype for enums */
	MonoClass* element_class; 
	/* used for subtype checks */
	MonoClass* cast_class; 

	/* for fast subtype checks */
	MonoClass** supertypes;
	ushort     idepth;

	/* array dimension */
	byte     rank;          

	int        instance_size; /* object instance size */

	uint inited          : 1;

	/* A class contains static and non static data. Static data can be
	 * of the same type as the class itself, but it does not influence
	 * the instance size of the class. To avoid cyclic calls to 
	 * mono_class_init (from mono_class_instance_size ()) we first 
	 * initialise all non static fields. After that we set size_inited 
	 * to 1, because we know the instance size now. After that we 
	 * initialise all static fields.
	 */

	/* ALL BITFIELDS SHOULD BE WRITTEN WHILE HOLDING THE LOADER LOCK */
	uint size_inited     : 1;
	uint valuetype       : 1; /* derives from System.ValueType */
	uint enumtype        : 1; /* derives from System.Enum */
	uint blittable       : 1; /* class is blittable */
	uint unicode         : 1; /* class uses unicode char when marshalled */
	uint wastypebuilder  : 1; /* class was created at runtime from a TypeBuilder */
	uint is_array_special_interface : 1; /* gtd or ginst of once of the magic interfaces that arrays implement */

	/* next byte */
	byte min_align;

	/* next byte */
	uint packing_size    : 4;
	uint ghcimpl         : 1; /* class has its own GetHashCode impl */ 
	uint has_finalize    : 1; /* class has its own Finalize impl */ 
	uint marshalbyref    : 1; /* class is a MarshalByRefObject */
	uint contextbound    : 1; /* class is a ContextBoundObject */
	/* next byte */
	uint @delegate        : 1; /* class is a Delegate */
	uint gc_descr_inited : 1; /* gc_descr is initialized */
	uint has_cctor       : 1; /* class has a cctor */
	uint has_references  : 1; /* it has GC-tracked references in the instance */
	uint has_static_refs : 1; /* it has static fields that are GC-tracked */
	uint no_special_static_fields : 1; /* has no thread/context static fields */
	/* directly or indirectly derives from ComImport attributed class.
	 * this means we need to create a proxy for instances of this class
	 * for COM Interop. set this flag on loading so all we need is a quick check
	 * during object creation rather than having to traverse supertypes
	 */
	uint is_com_object : 1; 
	uint nested_classes_inited : 1; /* Whenever nested_class is initialized */

	/* next byte*/
	uint class_kind : 3; /* One of the values from MonoTypeKind */
	uint interfaces_inited : 1; /* interfaces is initialized */
	uint simd_type : 1; /* class is a simd intrinsic type */
	uint has_finalize_inited    : 1; /* has_finalize is initialized */
	uint fields_inited : 1; /* setup_fields () has finished */
	uint has_failure : 1; /* See mono_class_get_exception_data () for a MonoErrorBoxed with the details */
	uint has_weak_fields : 1; /* class has weak reference fields */

	MonoClass* parent;
	MonoClass* nested_in;

	MonoImage* image;
	const char* name;
	const char* name_space;

	uint    type_token;
	int        vtable_size; /* number of slots */

	ushort     interface_count;
	uint     interface_id;        /* unique inderface id (for interfaces) */
	uint     max_interface_id;
	
	ushort     interface_offsets_count;
	MonoClass** interfaces_packed;
	ushort* interface_offsets_packed;
/* enabled only with small config for now: we might want to do it unconditionally */
	byte* interface_bitmap;

	MonoClass** interfaces;

	[StructLayout(LayoutKind.Explicit)] // union
	struct sizes
	{
		[FieldOffset(0)]
		int class_size; /* size of area for static fields */
		[FieldOffset(0)]
		int element_size; /* for array types */
		[FieldOffset(0)]
		int generic_param_token; /* for generic param types, both var and mvar */
	}

	/*
	 * Field information: Type and location from object base
	 */
	MonoClassField* fields;

	MonoMethod** methods;

	/* used as the type of the this argument and when passing the arg by value */
	MonoType this_arg;
	MonoType byval_arg;

	MonoGCDescriptor gc_descr;

	MonoClassRuntimeInfo *runtime_info;

	/* Generic vtable. Initialized by a call to mono_class_setup_vtable () */
	MonoMethod** vtable;

	/* Infrequently used items. See class-accessors.c: InfrequentDataKind for what goes into here. */
	MonoPropertyBag infrequent_data;

	void* unity_user_data;
}

/* the interface_offsets array is stored in memory before this struct */
unsafe struct MonoVTable
{
	MonoClass* klass;
	/*
	* According to comments in gc_gcj.h, this should be the second word in
	* the vtable.
	*/
	MonoGCDescriptor gc_descr;
	MonoDomain* domain;  /* each object/vtable belongs to exactly one domain */
	nint type; /* System.Type type for klass */
	byte* interface_bitmap;
	uint max_interface_id;
	byte rank;
	/* Keep this a guint8, the jit depends on it */
	byte      initialized; /* cctor has been run */
	uint remote          : 1; /* class is remotely activated */
	uint init_failed     : 1; /* cctor execution failed */
	uint has_static_fields : 1; /* pointer to the data stored at the end of the vtable array */
	uint gc_bits         : MONO_VTABLE_AVAILABLE_GC_BITS; /* Those bits are reserved for the usaged of the GC */

	uint     imt_collisions_bitmap;
	MonoRuntimeGenericContext* runtime_generic_context;
	/* do not add any fields after vtable, the structure is dynamically extended */
	/* vtable contains function pointers to methods or their trampolines, at the
	 end there may be a slot containing the pointer to the static fields */
	nint    vtable [MONO_ZERO_LEN_ARRAY];	
}
#endif