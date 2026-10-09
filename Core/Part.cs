namespace Core;

public class DisplaySpec
{
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class Part
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float Mass { get; set; }
    public int Cost { get; set; }
    public int CrewCapacity { get; set; }
    public float MaxTemp { get; set; }
    public float SkinMaxTemp { get; set; }
    
    public string Category { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ModOrigin { get; set; } = string.Empty;
    public List<string> PatchLog { get; set; } = [];
    
    public List<PartModule> Modules { get; set; } = [];
    public List<PartResource> Resources { get; set; } = [];
    
    private static readonly Dictionary<string, (string Group, Dictionary<string, string> FieldLabels)> ModuleMapping = 
        new(StringComparer.OrdinalIgnoreCase)
        {
            { 
                "ModuleEnginesFX", 
                ("PROPULSION", new Dictionary<string, string> { { "maxThrust", "MAX THRUST" }, { "engineType", "ENGINE TYPE" } }) 
            },
            { 
                "ModuleEngines", 
                ("PROPULSION", new Dictionary<string, string> { { "maxThrust", "MAX THRUST" }, { "engineType", "ENGINE TYPE" } }) 
            },
            { 
                "ModuleDeployableSolarPanel", 
                ("ELECTRICAL", new Dictionary<string, string> { { "chargeRate", "SOLAR OUTPUT" } }) 
            },
            { 
                "ModuleSolarPanel", 
                ("ELECTRICAL", new Dictionary<string, string> { { "chargeRate", "SOLAR OUTPUT" } }) 
            },
            { 
                "ModuleReactionWheel", 
                ("ATTITUDE CONTROL", new Dictionary<string, string> { { "pitchTorque", "PITCH TORQUE" }, { "yawTorque", "YAW TORQUE" }, { "rollTorque", "ROLL TORQUE" } }) 
            },
            {
                "ModuleGimbal",
                ("ATTITUDE CONTROL", new Dictionary<string, string> { { "gimbalRange", "GIMBAL RANGE" }, { "gimbalResponseSpeed", "RESPONSE SPEED" } })
            },
            { 
                "ModuleCommand", 
                ("COMMAND SYSTEMS", new Dictionary<string, string> { { "minimumCrew", "MIN REQUIRED CREW" }, { "crewCapacity", "CREW CAPACITY" } })     
            },
            { 
                "ModuleDataTransmitter", 
                ("COMMUNICATIONS", new Dictionary<string, string> { { "antennaPower", "ANTENNA POWER" }, { "antennaType", "TRANSMITTER TYPE" }, { "packetSize", "PACKET SIZE" }, { "packetResourceCost", "PACKET COST" } }) 
            },
            {
                "ModuleRCSFX",
                ("ATTITUDE CONTROL", new Dictionary<string, string> { { "thrusterPower", "RCS THRUST" } })
            },
            {
                "ModuleControlSurface",
                ("AERODYNAMICS", new Dictionary<string, string> { { "deflectionLiftCoeff", "LIFT COEFFICIENT" }, { "ctrlSurfaceArea", "CONTROL SURFACE AREA" } })
            },
            {
                "ModuleLiftingSurface",
                ("AERODYNAMICS", new Dictionary<string, string> { { "deflectionLiftCoeff", "LIFT COEFFICIENT" } })
            },
            {
                "ModuleAblator",
                ("THERMAL", new Dictionary<string, string> { { "ablationTempThresh", "ABLATION THRESHOLD" }, { "lossConst", "ABLATION LOSS CONST" } })
            },
            {
                "ModuleDeployableRadiator",
                ("THERMAL", new Dictionary<string, string> { { "impactResistance", "DEPLOYED IMPACT RESISTANCE" }, { "trackingSpeed", "TRACKING SPEED" }, { "windResistance", "WIND RESISTANCE" } })
            },
            {
                "ModuleActiveRadiator",
                ("THERMAL", new Dictionary<string, string> { { "maxEnergyTransfer", "MAX HEAT TRANSFER" } })
            },
            {
                "ModuleProceduralFairing",
                ("PAYLOAD", new Dictionary<string, string> { { "ejectionForce", "EJECTION FORCE" } })
            },
            {
                "ModuleRoboticServoPiston",
                ("ROBOTICS", new Dictionary<string, string> { { "maxMotorOutput", "MAX MOTOR OUTPUT" }, { "baseResourceConsumptionRate", "POWER DRAW" } })
            },
            {
                "ModuleRoboticServoRotor",
                ("ROBOTICS", new Dictionary<string, string> { { "maxMotorOutput", "MAX MOTOR OUTPUT" }, { "baseResourceConsumptionRate", "POWER DRAW" }, { "rpmLimit", "RPM LIMIT" } })
            },
            {
                "ModuleWheelBase",
                ("GROUND", new Dictionary<string, string> { { "groundHeightOffset", "GROUND HEIGHT OFFSET" } })
            },
            {
                "ModuleWheelSuspension",
                ("GROUND", new Dictionary<string, string> { { "suspensionDistance", "SUSPENSION TRAVEL" } })
            }
        };

    public List<DisplaySpec> ImportantSpecs
    {
        get
        {
            var specs = new List<DisplaySpec>();
            if (specs == null) throw new ArgumentNullException(nameof(specs));
            if (CrewCapacity > 0)
            {
                specs.Add(new DisplaySpec
                {
                    Group = "COMMAND SYSTEMS",
                    Label = "CREW CAPACITY",
                    Value = CrewCapacity.ToString()
                });
            }

            if (MaxTemp > 0)
            {
                specs.Add(new DisplaySpec
                {
                    Group = "THERMAL",
                    Label = "MAX TEMPERATURE",
                    Value = $"{MaxTemp:0.#} K"
                });
            }

            if (SkinMaxTemp > 0)
            {
                specs.Add(new DisplaySpec
                {
                    Group = "THERMAL",
                    Label = "SKIN MAX TEMPERATURE",
                    Value = $"{SkinMaxTemp:0.#} K"
                });
            }

            foreach (var module in Modules)
            {
                if (!ModuleMapping.TryGetValue(module.Name, out var mapping)) continue;
                foreach (var (rawField, userLabel) in mapping.FieldLabels)
                {
                    if (!module.Fields.TryGetValue(rawField, out var value)) continue;
                    var finalizedValue = value;
                    switch (mapping.Group)
                    {
                        case "PROPULSION" when rawField == "maxThrust":
                            finalizedValue += " kN";
                            break;
                        case "ELECTRICAL":
                            finalizedValue += " Ec/s";
                            break;
                        case "ATTITUDE CONTROL":
                            if (rawField.EndsWith("Torque", StringComparison.OrdinalIgnoreCase))
                            {
                                finalizedValue += " kNm";
                            }
                            else switch (rawField)
                            {
                                case "gimbalRange":
                                    finalizedValue += " deg";
                                    break;
                                case "thrusterPower":
                                    finalizedValue += " kN";
                                    break;
                            }
                            break;
                        case "AERODYNAMICS" when rawField == "ctrlSurfaceArea":
                            finalizedValue += " m²";
                            break;
                        case "THERMAL" when rawField == "ablationTempThresh":
                            finalizedValue += " K";
                            break;
                        case "THERMAL" when rawField == "maxEnergyTransfer":
                            finalizedValue += " kW";
                            break;
                        case "THERMAL" when rawField == "trackingSpeed":
                            finalizedValue += " /s";
                            break;
                        case "PAYLOAD" when rawField == "ejectionForce":
                            finalizedValue += " kN";
                            break;
                        case "ROBOTICS" when rawField == "maxMotorOutput":
                            finalizedValue += " kN";
                            break;
                        case "ROBOTICS" when rawField == "baseResourceConsumptionRate":
                            finalizedValue += " Ec/s";
                            break;
                        case "ROBOTICS" when rawField == "rpmLimit":
                            finalizedValue += " rpm";
                            break;
                        case "GROUND":
                            finalizedValue += " m";
                            break;
                    }

                    specs.Add(new DisplaySpec
                    {
                        Group = mapping.Group,
                        Label = userLabel,
                        Value = finalizedValue
                    });
                }
            }

            return specs;
        }
    }

    public bool HasImportantSpecs => ImportantSpecs.Count != 0;
}