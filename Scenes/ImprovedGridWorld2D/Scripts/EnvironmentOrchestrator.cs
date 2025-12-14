using GridWorld.Generation;
using System;
using Unity.MLAgents;
using UnityEngine;
using SideChannels;

namespace Core
{
    public class EnvironmentOrchestrator : MonoBehaviour
    {
        [SerializeField] string environmentsAmountKey = "env_amount";
        [SerializeField] int environmentsAmount = 1;
        [SerializeField] int maxEnvironmentsPerRow = 10;
        [SerializeField] int maxEnvironmentsPerCol = 10;
        [SerializeField] float environmentGap = 10f;

        [Header("Environment Prefab")]
        [SerializeField] GameObject environment;

        private void Awake()
        {
            var environmentsSettings = FindFirstObjectByType<SettingsBase>();
            
            SideChannelRegistrar registrar = this.environment.GetComponent<SideChannelRegistrar>();

            if (registrar == null)
            {
                throw new MissingReferenceException("The environment prefab must have a SideChannelRegistrar component attached to it");
            }

            if (environmentsSettings == null)
            {
                throw new NullReferenceException("Error happened while instantiating environments, grid settings was not present at scene");
            }

            this.environmentsAmount = Mathf.CeilToInt(Academy.Instance.EnvironmentParameters.GetWithDefault(environmentsAmountKey, environmentsAmount));
            if (environmentsAmount < 1)
            {
                throw new ArgumentException("Environments amount can't be less then 1");
            }

            registrar.RegisterChannels();

            this.SetupEnvironments(environmentsSettings);
        }

        private void SetupEnvironments(SettingsBase environmentSettings)
        {
            Vector3 maxPhysicalSize = environmentSettings.GetMaxPhysicalSize();
            float unitSize = environmentSettings.GetUnitSize();

            for (int i = 0; i < environmentsAmount; i++)
            {
                int row = (i / this.maxEnvironmentsPerCol) % this.maxEnvironmentsPerRow;
                int col = i % this.maxEnvironmentsPerRow;
                int layer = i / (this.maxEnvironmentsPerRow * this.maxEnvironmentsPerCol);

                float baseSpacing = (unitSize * 2) + this.environmentGap;
                float spacingX = maxPhysicalSize.x + baseSpacing;
                float spacingY = maxPhysicalSize.y + baseSpacing;
                float spacingZ = -(maxPhysicalSize.z + baseSpacing);

                Vector3 pos = new Vector3(spacingX * col, spacingY * layer, spacingZ * row);

                Instantiate(this.environment, pos, Quaternion.identity);
            }
        }
    }
}