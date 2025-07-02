using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor.SceneManagement;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Unified MCP tool for particle system management in Unity
    /// </summary>
    [McpServerToolType, Description("Unified particle system management tools for Unity")]
    internal sealed class McpUnifiedEffectTool : McpToolBase
    {

        [McpServerTool, Description("Control particle system playback")]
        public async ValueTask<string> ControlParticleSystem(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Action to perform: 'play' or 'stop'")]
            string action,
            [Description("Control in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                await ValidatePrefabMode(inPrefabMode);

                var particleSystem = FindParticleSystem(objectName, inPrefabMode);
                if (particleSystem == null)
                    return McpToolUtilities.CreateErrorMessage($"No ParticleSystem found on GameObject '{objectName}'{McpToolUtilities.GetContextDescription(inPrefabMode)}");

                switch (action?.ToLower())
                {
                    case "play":
                        particleSystem.Play();
                        return McpToolUtilities.CreateSuccessMessage($"Started ParticleSystem on '{objectName}'");

                    case "stop":
                        particleSystem.Stop();
                        return McpToolUtilities.CreateSuccessMessage($"Stopped ParticleSystem on '{objectName}'");

                    default:
                        return McpToolUtilities.CreateErrorMessage("action must be 'play' or 'stop'");
                }
            }, "controlling particle system");
        }

        [McpServerTool, Description("Get detailed information about a particle system")]
        public async ValueTask<string> GetParticleSystemInfo(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Include detailed module information (optional, default: true)")]
            bool includeModuleDetails = true,
            [Description("Get info from Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                await ValidatePrefabMode(inPrefabMode);

                var particleSystem = FindParticleSystem(objectName, inPrefabMode);
                if (particleSystem == null)
                    return McpToolUtilities.CreateErrorMessage($"No ParticleSystem found on GameObject '{objectName}'{McpToolUtilities.GetContextDescription(inPrefabMode)}");

                var info = GetBasicParticleSystemInfo(particleSystem, objectName);

                if (includeModuleDetails)
                {
                    AddModuleDetails(info, particleSystem);
                }

                return JsonConvert.SerializeObject(info, Formatting.Indented);
            }, "getting particle system info");
        }

        private Dictionary<string, object> GetBasicParticleSystemInfo(ParticleSystem particleSystem, string objectName)
        {
            return new Dictionary<string, object>
            {
                ["objectName"] = objectName,
                ["particleCount"] = particleSystem.particleCount,
                ["isPlaying"] = particleSystem.isPlaying,
                ["isPaused"] = particleSystem.isPaused,
                ["isStopped"] = particleSystem.isStopped,
                ["isEmitting"] = particleSystem.isEmitting,
                ["time"] = particleSystem.time
            };
        }

        private void AddModuleDetails(Dictionary<string, object> info, ParticleSystem particleSystem)
        {
            info["mainModule"] = GetMainModuleInfo(particleSystem.main);

            info["shapeModule"] = GetShapeModuleInfo(particleSystem.shape);

            info["emissionModule"] = GetEmissionModuleInfo(particleSystem.emission);

            info["velocityOverLifetimeModule"] = GetVelocityOverLifetimeModuleInfo(particleSystem.velocityOverLifetime);

            info["colorOverLifetimeModule"] = GetColorOverLifetimeModuleInfo(particleSystem.colorOverLifetime);
            info["sizeOverLifetimeModule"] = GetSizeOverLifetimeModuleInfo(particleSystem.sizeOverLifetime);
            info["rotationOverLifetimeModule"] = GetRotationOverLifetimeModuleInfo(particleSystem.rotationOverLifetime);
            info["forceOverLifetimeModule"] = GetForceOverLifetimeModuleInfo(particleSystem.forceOverLifetime);

            info["noiseModule"] = GetNoiseModuleInfo(particleSystem.noise);

            info["externalForcesModule"] = GetExternalForcesModuleInfo(particleSystem.externalForces);

            info["collisionModule"] = GetCollisionModuleInfo(particleSystem.collision);

            info["subEmittersModule"] = GetSubEmittersModuleInfo(particleSystem.subEmitters);
            info["textureSheetAnimationModule"] = GetTextureSheetAnimationModuleInfo(particleSystem.textureSheetAnimation);
            info["lightsModule"] = GetLightsModuleInfo(particleSystem.lights);
            info["trailsModule"] = GetTrailsModuleInfo(particleSystem.trails);
            info["customDataModule"] = GetCustomDataModuleInfo(particleSystem.customData);

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                info["renderer"] = GetRendererInfo(renderer);
            }
        }

        private object GetMinMaxCurveValue(ParticleSystem.MinMaxCurve curve)
        {
            var result = new Dictionary<string, object>
            {
                ["mode"] = curve.mode.ToString()
            };

            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    result["constant"] = curve.constant;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    result["constantMin"] = curve.constantMin;
                    result["constantMax"] = curve.constantMax;
                    break;
                case ParticleSystemCurveMode.Curve:
                    result["curveMultiplier"] = curve.curveMultiplier;
                    result["curveKeys"] = curve.curve?.keys?.Length ?? 0;
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    result["curveMultiplier"] = curve.curveMultiplier;
                    result["curveMinKeys"] = curve.curveMin?.keys?.Length ?? 0;
                    result["curveMaxKeys"] = curve.curveMax?.keys?.Length ?? 0;
                    break;
            }

            return result;
        }

        private object GetMinMaxGradientValue(ParticleSystem.MinMaxGradient gradient)
        {
            var result = new Dictionary<string, object>
            {
                ["mode"] = gradient.mode.ToString()
            };

            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    var color = gradient.color;
                    result["color"] = new float[] { color.r, color.g, color.b, color.a };
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    var minColor = gradient.colorMin;
                    var maxColor = gradient.colorMax;
                    result["colorMin"] = new float[] { minColor.r, minColor.g, minColor.b, minColor.a };
                    result["colorMax"] = new float[] { maxColor.r, maxColor.g, maxColor.b, maxColor.a };
                    break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.TwoGradients:
                    result["gradientKeys"] = gradient.gradient?.colorKeys?.Length ?? 0;
                    break;
                case ParticleSystemGradientMode.RandomColor:
                    result["randomColor"] = true;
                    break;
            }

            return result;
        }

        private object Vector3ToArray(Vector3 v)
        {
            return new float[] { v.x, v.y, v.z };
        }

        private object Vector2ToArray(Vector2 v)
        {
            return new float[] { v.x, v.y };
        }

        private ParticleSystem FindParticleSystem(string objectName, bool inPrefabMode = false)
        {
            GameObject gameObject = McpToolUtilities.FindGameObject(objectName, inPrefabMode);
            
            if (gameObject == null)
                return null;

            // First check the object itself
            var particleSystem = gameObject.GetComponent<ParticleSystem>();
            if (particleSystem != null)
                return particleSystem;

            // Then check children
            return gameObject.GetComponentInChildren<ParticleSystem>();
        }

        private void AssignDefaultMaterial(ParticleSystem particleSystem)
        {
            try
            {
                var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                    return;

                // Check if material is already assigned
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.name != "Default-Material")
                    return;

                // Try to find appropriate default materials in the project
                Material defaultMaterial = null;

                // First, try to find a specific particle material
                var particleMaterialGUIDs = AssetDatabase.FindAssets("t:Material Particle", new[] { "Assets" });
                if (particleMaterialGUIDs.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(particleMaterialGUIDs[0]);
                    defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                }

                // If no particle material found, try Default-Particle material
                if (defaultMaterial == null)
                {
                    defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
                }

                // If still no material, try Sprites-Default material which works well for particles
                if (defaultMaterial == null)
                {
                    defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                }

                // As a last resort, create a simple unlit material
                if (defaultMaterial == null)
                {
                    defaultMaterial = new Material(Shader.Find("Sprites/Default"));
                    defaultMaterial.name = "Particle Default Material";

                    // Save it as an asset
                    if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    {
                        AssetDatabase.CreateFolder("Assets", "Materials");
                    }
                    AssetDatabase.CreateAsset(defaultMaterial, "Assets/Materials/ParticleDefaultMaterial.mat");
                }

                if (defaultMaterial != null)
                {
                    renderer.sharedMaterial = defaultMaterial;
                    EditorUtility.SetDirty(renderer);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to assign default material to particle system: {e.Message}");
            }
        }

        // Module info methods
        private Dictionary<string, object> GetMainModuleInfo(ParticleSystem.MainModule main)
        {
            return new Dictionary<string, object>
            {
                ["duration"] = main.duration,
                ["loop"] = main.loop,
                ["prewarm"] = main.prewarm,
                ["startDelay"] = GetMinMaxCurveValue(main.startDelay),
                ["startLifetime"] = GetMinMaxCurveValue(main.startLifetime),
                ["startSpeed"] = GetMinMaxCurveValue(main.startSpeed),
                ["startSize"] = GetMinMaxCurveValue(main.startSize),
                ["startRotation"] = GetMinMaxCurveValue(main.startRotation),
                ["flipRotation"] = main.flipRotation,
                ["startColor"] = GetMinMaxGradientValue(main.startColor),
                ["gravityModifier"] = GetMinMaxCurveValue(main.gravityModifier),
                ["simulationSpace"] = main.simulationSpace.ToString(),
                ["simulationSpeed"] = main.simulationSpeed,
                ["scalingMode"] = main.scalingMode.ToString(),
                ["playOnAwake"] = main.playOnAwake,
                ["maxParticles"] = main.maxParticles,
                ["emitterVelocityMode"] = main.emitterVelocityMode.ToString(),
                ["stopAction"] = main.stopAction.ToString()
            };
        }

        private Dictionary<string, object> GetShapeModuleInfo(ParticleSystem.ShapeModule shape)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = shape.enabled,
                ["shapeType"] = shape.shapeType.ToString(),
                ["angle"] = shape.angle,
                ["radius"] = shape.radius,
                ["radiusThickness"] = shape.radiusThickness,
                ["arc"] = shape.arc,
                ["arcMode"] = shape.arcMode.ToString(),
                ["arcSpread"] = shape.arcSpread,
                ["rotation"] = Vector3ToArray(shape.rotation),
                ["scale"] = Vector3ToArray(shape.scale),
                ["position"] = Vector3ToArray(shape.position),
                ["alignToDirection"] = shape.alignToDirection,
                ["randomDirectionAmount"] = shape.randomDirectionAmount,
                ["sphericalDirectionAmount"] = shape.sphericalDirectionAmount
            };
        }

        private Dictionary<string, object> GetEmissionModuleInfo(ParticleSystem.EmissionModule emission)
        {
            var emissionInfo = new Dictionary<string, object>
            {
                ["enabled"] = emission.enabled,
                ["rateOverTime"] = GetMinMaxCurveValue(emission.rateOverTime),
                ["rateOverDistance"] = GetMinMaxCurveValue(emission.rateOverDistance),
                ["burstCount"] = emission.burstCount
            };
            
            // Get burst information
            if (emission.burstCount > 0)
            {
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                var burstList = new List<Dictionary<string, object>>();
                foreach (var burst in bursts)
                {
                    burstList.Add(new Dictionary<string, object>
                    {
                        ["time"] = burst.time,
                        ["count"] = GetMinMaxCurveValue(burst.count),
                        ["cycleCount"] = burst.cycleCount,
                        ["repeatInterval"] = burst.repeatInterval,
                        ["probability"] = burst.probability
                    });
                }
                emissionInfo["bursts"] = burstList;
            }
            return emissionInfo;
        }

        private Dictionary<string, object> GetVelocityOverLifetimeModuleInfo(ParticleSystem.VelocityOverLifetimeModule velocity)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = velocity.enabled,
                ["space"] = velocity.space.ToString(),
                ["x"] = GetMinMaxCurveValue(velocity.x),
                ["y"] = GetMinMaxCurveValue(velocity.y),
                ["z"] = GetMinMaxCurveValue(velocity.z),
                ["speedModifier"] = GetMinMaxCurveValue(velocity.speedModifier),
                ["orbitalX"] = GetMinMaxCurveValue(velocity.orbitalX),
                ["orbitalY"] = GetMinMaxCurveValue(velocity.orbitalY),
                ["orbitalZ"] = GetMinMaxCurveValue(velocity.orbitalZ),
                ["orbitalOffsetX"] = GetMinMaxCurveValue(velocity.orbitalOffsetX),
                ["orbitalOffsetY"] = GetMinMaxCurveValue(velocity.orbitalOffsetY),
                ["orbitalOffsetZ"] = GetMinMaxCurveValue(velocity.orbitalOffsetZ),
                ["radial"] = GetMinMaxCurveValue(velocity.radial)
            };
        }

        private Dictionary<string, object> GetColorOverLifetimeModuleInfo(ParticleSystem.ColorOverLifetimeModule colorOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = colorOverLifetime.enabled,
                ["color"] = GetMinMaxGradientValue(colorOverLifetime.color)
            };
        }

        private Dictionary<string, object> GetSizeOverLifetimeModuleInfo(ParticleSystem.SizeOverLifetimeModule sizeOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = sizeOverLifetime.enabled,
                ["size"] = GetMinMaxCurveValue(sizeOverLifetime.size),
                ["sizeMultiplier"] = sizeOverLifetime.sizeMultiplier,
                ["x"] = GetMinMaxCurveValue(sizeOverLifetime.x),
                ["y"] = GetMinMaxCurveValue(sizeOverLifetime.y),
                ["z"] = GetMinMaxCurveValue(sizeOverLifetime.z),
                ["separateAxes"] = sizeOverLifetime.separateAxes
            };
        }

        private Dictionary<string, object> GetRotationOverLifetimeModuleInfo(ParticleSystem.RotationOverLifetimeModule rotationOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = rotationOverLifetime.enabled,
                ["x"] = GetMinMaxCurveValue(rotationOverLifetime.x),
                ["y"] = GetMinMaxCurveValue(rotationOverLifetime.y),
                ["z"] = GetMinMaxCurveValue(rotationOverLifetime.z),
                ["separateAxes"] = rotationOverLifetime.separateAxes
            };
        }

        private Dictionary<string, object> GetForceOverLifetimeModuleInfo(ParticleSystem.ForceOverLifetimeModule forceOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = forceOverLifetime.enabled,
                ["space"] = forceOverLifetime.space.ToString(),
                ["x"] = GetMinMaxCurveValue(forceOverLifetime.x),
                ["y"] = GetMinMaxCurveValue(forceOverLifetime.y),
                ["z"] = GetMinMaxCurveValue(forceOverLifetime.z),
                ["randomized"] = forceOverLifetime.randomized
            };
        }

        private Dictionary<string, object> GetNoiseModuleInfo(ParticleSystem.NoiseModule noise)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = noise.enabled,
                ["strength"] = GetMinMaxCurveValue(noise.strength),
                ["strengthX"] = GetMinMaxCurveValue(noise.strengthX),
                ["strengthY"] = GetMinMaxCurveValue(noise.strengthY),
                ["strengthZ"] = GetMinMaxCurveValue(noise.strengthZ),
                ["frequency"] = noise.frequency,
                ["damping"] = noise.damping,
                ["octaveCount"] = noise.octaveCount,
                ["octaveMultiplier"] = noise.octaveMultiplier,
                ["octaveScale"] = noise.octaveScale,
                ["quality"] = noise.quality.ToString(),
                ["scrollSpeed"] = GetMinMaxCurveValue(noise.scrollSpeed),
                ["remapEnabled"] = noise.remapEnabled,
                ["remap"] = GetMinMaxCurveValue(noise.remap),
                ["remapX"] = GetMinMaxCurveValue(noise.remapX),
                ["remapY"] = GetMinMaxCurveValue(noise.remapY),
                ["remapZ"] = GetMinMaxCurveValue(noise.remapZ),
                ["positionAmount"] = GetMinMaxCurveValue(noise.positionAmount),
                ["rotationAmount"] = GetMinMaxCurveValue(noise.rotationAmount),
                ["sizeAmount"] = GetMinMaxCurveValue(noise.sizeAmount)
            };
        }

        private Dictionary<string, object> GetExternalForcesModuleInfo(ParticleSystem.ExternalForcesModule externalForces)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = externalForces.enabled,
                ["multiplier"] = externalForces.multiplier,
                ["multiplierCurve"] = GetMinMaxCurveValue(externalForces.multiplierCurve),
                ["influenceFilter"] = externalForces.influenceFilter.ToString(),
                ["influenceCount"] = externalForces.influenceCount
            };
        }

        private Dictionary<string, object> GetCollisionModuleInfo(ParticleSystem.CollisionModule collision)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = collision.enabled,
                ["type"] = collision.type.ToString(),
                ["mode"] = collision.mode.ToString(),
                ["dampen"] = GetMinMaxCurveValue(collision.dampen),
                ["bounce"] = GetMinMaxCurveValue(collision.bounce),
                ["lifetimeLoss"] = GetMinMaxCurveValue(collision.lifetimeLoss),
                ["minKillSpeed"] = collision.minKillSpeed,
                ["maxKillSpeed"] = collision.maxKillSpeed,
                ["collidesWith"] = collision.collidesWith.value,
                ["enableDynamicColliders"] = collision.enableDynamicColliders,
                ["maxCollisionShapes"] = collision.maxCollisionShapes,
                ["quality"] = collision.quality.ToString(),
                ["voxelSize"] = collision.voxelSize,
                ["radiusScale"] = collision.radiusScale,
                ["sendCollisionMessages"] = collision.sendCollisionMessages
            };
        }

        private Dictionary<string, object> GetSubEmittersModuleInfo(ParticleSystem.SubEmittersModule subEmitters)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = subEmitters.enabled,
                ["subEmittersCount"] = subEmitters.subEmittersCount
            };
        }

        private Dictionary<string, object> GetTextureSheetAnimationModuleInfo(ParticleSystem.TextureSheetAnimationModule textureSheetAnimation)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = textureSheetAnimation.enabled,
                ["mode"] = textureSheetAnimation.mode.ToString(),
                ["numTilesX"] = textureSheetAnimation.numTilesX,
                ["numTilesY"] = textureSheetAnimation.numTilesY,
                ["animation"] = textureSheetAnimation.animation.ToString(),
                ["frameOverTime"] = GetMinMaxCurveValue(textureSheetAnimation.frameOverTime),
                ["startFrame"] = GetMinMaxCurveValue(textureSheetAnimation.startFrame),
                ["cycleCount"] = textureSheetAnimation.cycleCount,
                ["rowIndex"] = textureSheetAnimation.rowIndex,
                ["uvChannelMask"] = textureSheetAnimation.uvChannelMask.ToString()
            };
        }

        private Dictionary<string, object> GetLightsModuleInfo(ParticleSystem.LightsModule lights)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = lights.enabled,
                ["ratio"] = lights.ratio,
                ["useRandomDistribution"] = lights.useRandomDistribution,
                ["light"] = lights.light != null ? lights.light.name : "None",
                ["useParticleColor"] = lights.useParticleColor,
                ["sizeAffectsRange"] = lights.sizeAffectsRange,
                ["alphaAffectsIntensity"] = lights.alphaAffectsIntensity,
                ["range"] = GetMinMaxCurveValue(lights.range),
                ["rangeMultiplier"] = lights.rangeMultiplier,
                ["intensity"] = GetMinMaxCurveValue(lights.intensity),
                ["intensityMultiplier"] = lights.intensityMultiplier,
                ["maxLights"] = lights.maxLights
            };
        }

        private Dictionary<string, object> GetTrailsModuleInfo(ParticleSystem.TrailModule trails)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = trails.enabled,
                ["mode"] = trails.mode.ToString(),
                ["ratio"] = trails.ratio,
                ["lifetime"] = GetMinMaxCurveValue(trails.lifetime),
                ["lifetimeMultiplier"] = trails.lifetimeMultiplier,
                ["minVertexDistance"] = trails.minVertexDistance,
                ["textureMode"] = trails.textureMode.ToString(),
                ["worldSpace"] = trails.worldSpace,
                ["dieWithParticles"] = trails.dieWithParticles,
                ["sizeAffectsWidth"] = trails.sizeAffectsWidth,
                ["sizeAffectsLifetime"] = trails.sizeAffectsLifetime,
                ["inheritParticleColor"] = trails.inheritParticleColor,
                ["colorOverLifetime"] = GetMinMaxGradientValue(trails.colorOverLifetime),
                ["widthOverTrail"] = GetMinMaxCurveValue(trails.widthOverTrail),
                ["widthOverTrailMultiplier"] = trails.widthOverTrailMultiplier,
                ["colorOverTrail"] = GetMinMaxGradientValue(trails.colorOverTrail),
                ["generateLightingData"] = trails.generateLightingData,
                ["ribbonCount"] = trails.ribbonCount,
                ["shadowBias"] = trails.shadowBias,
                ["splitSubEmitterRibbons"] = trails.splitSubEmitterRibbons
            };
        }

        private Dictionary<string, object> GetCustomDataModuleInfo(ParticleSystem.CustomDataModule customData)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = customData.enabled
            };
        }

        private Dictionary<string, object> GetRendererInfo(ParticleSystemRenderer renderer)
        {
            return new Dictionary<string, object>
            {
                ["renderMode"] = renderer.renderMode.ToString(),
                ["material"] = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "None",
                ["trailMaterial"] = renderer.trailMaterial != null ? renderer.trailMaterial.name : "None",
                ["sortMode"] = renderer.sortMode.ToString(),
                ["sortingFudge"] = renderer.sortingFudge,
                ["minParticleSize"] = renderer.minParticleSize,
                ["maxParticleSize"] = renderer.maxParticleSize,
                ["alignment"] = renderer.alignment.ToString(),
                ["flip"] = Vector3ToArray(renderer.flip),
                ["allowRoll"] = renderer.allowRoll,
                ["pivot"] = Vector3ToArray(renderer.pivot),
                ["maskInteraction"] = renderer.maskInteraction.ToString(),
                ["enableGPUInstancing"] = renderer.enableGPUInstancing,
                ["shadowCastingMode"] = renderer.shadowCastingMode.ToString(),
                ["receiveShadows"] = renderer.receiveShadows,
                ["shadowBias"] = renderer.shadowBias,
                ["motionVectorGenerationMode"] = renderer.motionVectorGenerationMode.ToString(),
                ["sortingLayerID"] = renderer.sortingLayerID,
                ["sortingOrder"] = renderer.sortingOrder,
                ["lightProbeUsage"] = renderer.lightProbeUsage.ToString(),
                ["reflectionProbeUsage"] = renderer.reflectionProbeUsage.ToString()
            };
        }

        [McpServerTool, Description("Configure particle system with comprehensive settings")]
        public async ValueTask<string> ConfigureParticleSystem(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("Particle system configuration as structured JSON")]
            string configurationJson,
            [Description("Create new particle system if none exists")]
            bool createNew = false,
            [Description("Name for new particle system (optional, defaults to 'Particle System')")]
            string particleSystemName = null,
            [Description("Configure in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                await ValidatePrefabMode(inPrefabMode);

                var gameObject = await FindGameObjectSafe(objectName, inPrefabMode);
                var particleSystem = FindParticleSystem(objectName, inPrefabMode);

                if (particleSystem == null && createNew)
                {
                    var psGameObject = new GameObject(particleSystemName ?? "Particle System");
                    psGameObject.transform.SetParent(gameObject.transform);
                    psGameObject.transform.localPosition = Vector3.zero;
                    particleSystem = psGameObject.AddComponent<ParticleSystem>();
                    AssignDefaultMaterial(particleSystem);
                }
                else if (particleSystem == null)
                {
                    return McpToolUtilities.CreateErrorMessage($"No ParticleSystem found on GameObject '{objectName}'. Set createNew=true to create one.");
                }

                // Parse configuration using McpConfigurationManager
                if (!McpConfigurationManager.TryParseConfiguration<ParticleSystemConfiguration>(
                    configurationJson, out var config, out var validationResult))
                {
                    return McpToolUtilities.CreateErrorMessage($"Configuration validation failed: {validationResult}");
                }

                var changes = new List<string>();

                // Stop particle system if playing
                bool wasPlaying = particleSystem.isPlaying;
                if (wasPlaying)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                // Apply main module settings
                if (config.main != null)
                {
                    ApplyMainModuleSettings(particleSystem, config.main, changes);
                }

                // Apply emission settings
                if (config.emission != null)
                {
                    ApplyEmissionSettings(particleSystem, config.emission, changes);
                }

                // Apply shape settings
                if (config.shape != null)
                {
                    ApplyShapeSettings(particleSystem, config.shape, changes);
                }

                // Apply velocity settings
                if (config.velocityOverLifetime != null)
                {
                    ApplyVelocitySettings(particleSystem, config.velocityOverLifetime, changes);
                }

                // Mark objects as dirty
                EditorUtility.SetDirty(particleSystem.gameObject);
                MarkSceneDirty(inPrefabMode);

                // Restart if it was playing
                if (wasPlaying)
                {
                    particleSystem.Play();
                }

                return McpToolUtilities.CreateSuccessMessage(
                    $"Configured ParticleSystem on '{objectName}' with {changes.Count} changes: {string.Join(", ", changes)}");
            }, "configuring advanced particle system");
        }

        private void ApplyMainModuleSettings(ParticleSystem particleSystem, MainModuleSettings settings, List<string> changes)
        {
            var main = particleSystem.main;
            
            main.duration = settings.duration;
            main.loop = settings.looping;
            main.prewarm = settings.prewarm;
            main.startLifetime = settings.startLifetime;
            main.startSpeed = settings.startSpeed;
            main.startSize = settings.startSize;
            main.startRotation = settings.startRotation * Mathf.Deg2Rad;
            main.gravityModifier = settings.gravityModifier;
            main.maxParticles = settings.maxParticles;
            
            if (settings.startColor != null && settings.startColor.Length >= 4)
            {
                main.startColor = new Color(settings.startColor[0], settings.startColor[1], 
                                          settings.startColor[2], settings.startColor[3]);
            }

            changes.Add("main module");
        }

        private void ApplyEmissionSettings(ParticleSystem particleSystem, EmissionSettings settings, List<string> changes)
        {
            var emission = particleSystem.emission;
            
            emission.enabled = settings.enabled;
            emission.rateOverTime = settings.rateOverTime;
            emission.rateOverDistance = settings.rateOverDistance;

            if (settings.bursts != null && settings.bursts.Length > 0)
            {
                var bursts = new ParticleSystem.Burst[settings.bursts.Length];
                for (int i = 0; i < settings.bursts.Length; i++)
                {
                    var burstSetting = settings.bursts[i];
                    bursts[i] = new ParticleSystem.Burst(burstSetting.time, burstSetting.count, 
                                                       burstSetting.cycles, burstSetting.interval)
                    {
                        probability = burstSetting.probability
                    };
                }
                emission.SetBursts(bursts);
            }

            changes.Add("emission module");
        }

        private void ApplyShapeSettings(ParticleSystem particleSystem, ShapeSettings settings, List<string> changes)
        {
            var shape = particleSystem.shape;
            
            shape.enabled = settings.enabled;
            
            if (System.Enum.TryParse<ParticleSystemShapeType>(settings.shapeType, out var shapeType))
            {
                shape.shapeType = shapeType;
            }
            
            shape.angle = settings.angle;
            shape.radius = settings.radius;
            shape.radiusThickness = settings.radiusThickness;
            shape.arc = settings.arc;
            shape.length = settings.length;

            if (settings.position != null && settings.position.Length >= 3)
            {
                shape.position = new Vector3(settings.position[0], settings.position[1], settings.position[2]);
            }

            if (settings.rotation != null && settings.rotation.Length >= 3)
            {
                shape.rotation = new Vector3(settings.rotation[0], settings.rotation[1], settings.rotation[2]);
            }

            if (settings.scale != null && settings.scale.Length >= 3)
            {
                shape.scale = new Vector3(settings.scale[0], settings.scale[1], settings.scale[2]);
            }

            changes.Add("shape module");
        }

        private void ApplyVelocitySettings(ParticleSystem particleSystem, VelocitySettings settings, List<string> changes)
        {
            var velocity = particleSystem.velocityOverLifetime;
            
            velocity.enabled = settings.enabled;
            
            if (settings.linear != null && settings.linear.Length >= 3)
            {
                velocity.x = settings.linear[0];
                velocity.y = settings.linear[1];
                velocity.z = settings.linear[2];
            }

            if (settings.orbital != null && settings.orbital.Length >= 3)
            {
                velocity.orbitalX = settings.orbital[0];
                velocity.orbitalY = settings.orbital[1];
                velocity.orbitalZ = settings.orbital[2];
            }

            if (settings.offset != null && settings.offset.Length >= 3)
            {
                velocity.orbitalOffsetX = settings.offset[0];
                velocity.orbitalOffsetY = settings.offset[1];
                velocity.orbitalOffsetZ = settings.offset[2];
            }

            velocity.radial = settings.radial;
            velocity.speedModifier = settings.speedModifier;

            if (System.Enum.TryParse<ParticleSystemSimulationSpace>(settings.space, out var space))
            {
                velocity.space = space;
            }

            changes.Add("velocity over lifetime module");
        }

    }
}