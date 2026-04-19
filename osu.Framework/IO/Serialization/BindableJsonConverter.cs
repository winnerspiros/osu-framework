// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace osu.Framework.IO.Serialization
{
    /// <summary>
    /// A converter used for serializing/deserializing <see cref="Bindable{T}"/> objects.
    /// </summary>
    internal class BindableJsonConverter : JsonConverter<ISerializableBindable>
    {
        public override void WriteJson(JsonWriter writer, ISerializableBindable value, JsonSerializer serializer)
            => value.SerializeTo(writer, serializer);

        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Serialization types are preserved by the framework.")]
        public override ISerializableBindable ReadJson(JsonReader reader, Type objectType, ISerializableBindable existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var bindable = existingValue ?? (ISerializableBindable)Activator.CreateInstance(objectType, true);
            Debug.Assert(bindable != null);

            bindable.DeserializeFrom(reader, serializer);

            return bindable;
        }
    }
}
