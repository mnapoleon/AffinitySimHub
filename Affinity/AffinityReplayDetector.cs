using GameReaderCommon;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Affinity
{
    internal static class AffinityReplayDetector
    {
        private static readonly ConcurrentDictionary<Type, TypeMemberAccessorCache> MemberAccessors =
            new ConcurrentDictionary<Type, TypeMemberAccessorCache>();

        internal static bool IsReplay(GameData data)
        {
            // SimHub exposes replay state at runtime even though the local SDK stubs do not model every probe.
            if (data == null)
            {
                return false;
            }

            if (TryGetBooleanMemberValue(data, "IsGameReplay", out bool isGameReplay))
            {
                if (isGameReplay)
                {
                    return true;
                }
            }

            if (TryGetBooleanMemberValue(data, "GameReplay", out bool gameReplay))
            {
                if (gameReplay)
                {
                    return true;
                }
            }

            if (TryGetMemberValue(data, "ReplayMode", out object gameReplayModeValue))
            {
                if (IsReplayModeActive(gameReplayModeValue))
                {
                    return true;
                }
            }

            if (data.NewData == null)
            {
                return false;
            }

            if (TryGetBooleanMemberValue(data.NewData, "IsGameReplay", out bool statusReplay))
            {
                if (statusReplay)
                {
                    return true;
                }
            }

            if (TryGetMemberValue(data.NewData, "ReplayMode", out object statusReplayModeValue) &&
                IsReplayModeActive(statusReplayModeValue))
            {
                return true;
            }

            object rawData = GetRawStatusDataObject(data.NewData);
            if (TryGetBooleanMemberValue(rawData, "IsReplayPlaying", out bool rawReplayPlaying) && rawReplayPlaying)
            {
                return true;
            }

            if (TryGetMemberValue(rawData, "Telemetry", out object telemetry) &&
                TryGetBooleanMemberValue(telemetry, "IsReplayPlaying", out bool telemetryReplayPlaying) &&
                telemetryReplayPlaying)
            {
                return true;
            }

            return false;
        }

        internal static object GetRawStatusDataObject(StatusDataBase status)
        {
            if (status == null)
            {
                return null;
            }

            try
            {
                return status.GetRawDataObject();
            }
            catch
            {
                return null;
            }
        }

        internal static bool TryGetMemberValue(object source, string memberName, out object value)
        {
            value = null;
            if (source == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            TypeMemberAccessorCache accessors = MemberAccessors.GetOrAdd(
                source.GetType(),
                sourceType => new TypeMemberAccessorCache(sourceType));
            return accessors.TryGetValue(source, memberName, out value);
        }

        internal static bool TryGetBooleanMemberValue(object source, string memberName, out bool value)
        {
            value = false;
            return TryGetMemberValue(source, memberName, out object rawValue) &&
                TryGetBooleanValue(rawValue, out value);
        }

        internal static bool TryGetIntegerMemberValue(object source, string memberName, out int value)
        {
            value = 0;
            if (!TryGetMemberValue(source, memberName, out object rawValue) || rawValue == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetBooleanValue(object value, out bool result)
        {
            result = false;
            if (value == null)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                result = boolValue;
                return true;
            }

            if (value is string stringValue)
            {
                return bool.TryParse(stringValue, out result);
            }

            try
            {
                result = Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture)) > 0.0001;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsReplayModeActive(object replayModeValue)
        {
            if (replayModeValue == null)
            {
                return false;
            }

            if (replayModeValue is string || replayModeValue.GetType().IsEnum)
            {
                string replayModeText = replayModeValue.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(replayModeText))
                {
                    return false;
                }

                return !string.Equals(replayModeText, "None", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(replayModeText, "Off", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(replayModeText, "Disabled", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(replayModeText, "Live", StringComparison.OrdinalIgnoreCase);
            }

            return TryGetBooleanValue(replayModeValue, out bool replayModeFlag) &&
                replayModeFlag;
        }

        private sealed class TypeMemberAccessorCache
        {
            private const BindingFlags MemberFlags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.IgnoreCase;

            private readonly Type _sourceType;
            private readonly ConcurrentDictionary<string, MemberAccessor> _accessors =
                new ConcurrentDictionary<string, MemberAccessor>(StringComparer.OrdinalIgnoreCase);

            public TypeMemberAccessorCache(Type sourceType)
            {
                _sourceType = sourceType;
            }

            public bool TryGetValue(object source, string memberName, out object value)
            {
                MemberAccessor accessor = _accessors.GetOrAdd(memberName, CreateAccessor);
                return accessor.TryGetValue(source, out value);
            }

            private MemberAccessor CreateAccessor(string memberName)
            {
                PropertyInfo property = _sourceType.GetProperty(memberName, MemberFlags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return new MemberAccessor(property);
                }

                FieldInfo field = _sourceType.GetField(memberName, MemberFlags);
                return field != null
                    ? new MemberAccessor(field)
                    : MemberAccessor.Missing;
            }
        }

        private sealed class MemberAccessor
        {
            public static readonly MemberAccessor Missing = new MemberAccessor();

            private readonly PropertyInfo _property;
            private readonly FieldInfo _field;

            private MemberAccessor()
            {
            }

            public MemberAccessor(PropertyInfo property)
            {
                _property = property;
            }

            public MemberAccessor(FieldInfo field)
            {
                _field = field;
            }

            public bool TryGetValue(object source, out object value)
            {
                if (_property != null)
                {
                    value = _property.GetValue(source);
                    return true;
                }

                if (_field != null)
                {
                    value = _field.GetValue(source);
                    return true;
                }

                value = null;
                return false;
            }
        }
    }
}
