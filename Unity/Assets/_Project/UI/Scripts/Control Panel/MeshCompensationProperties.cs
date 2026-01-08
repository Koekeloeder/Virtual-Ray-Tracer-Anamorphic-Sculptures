using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.UI.Scripts.Control_Panel
{
    /// <summary>
    /// A UI class that provides access to the properties of a MeshCompensation component.
    /// Any changes made to the shown properties will be applied to the mesh compensation script.
    /// </summary>
    public class MeshCompensationProperties : MonoBehaviour
    {
        private MeshCompensation meshCompensation;

        [Header("Compensation Parameters")]
        [SerializeField]
        private FloatEdit minCompensationEdit;
        [SerializeField]
        private FloatEdit maxCompensationEdit;

        [Header("Lighting")]
        [SerializeField]
        private FloatEdit lightIntensityEdit;

        [Header("Anisotropy Parameters")]
        [SerializeField]
        private FloatEdit baseSmoothnessEdit;
        [SerializeField]
        private FloatEdit baseAnisotropyEdit;
        [SerializeField]
        private FloatEdit baseMetallicEdit;

        [Header("Mode Toggles")]
        [SerializeField]
        private BoolEdit colorCompensationEdit;
        [SerializeField]
        private BoolEdit enableAnisotropicEdit;
        [SerializeField]
        private BoolEdit autoUpdateEdit;

        /// <summary>
        /// Show the mesh compensation properties for the given MeshCompensation component.
        /// These properties can be changed via the shown UI.
        /// </summary>
        /// <param name="compensation"> The MeshCompensation whose properties will be shown. </param>
        public void Show(MeshCompensation compensation)
        {
            gameObject.SetActive(true);
            this.meshCompensation = compensation;

            // Set all UI values from the compensation component
            minCompensationEdit.Value = compensation.minCompensation;
            maxCompensationEdit.Value = compensation.maxCompensation;

            lightIntensityEdit.Value = compensation.lightIntensity;

            baseSmoothnessEdit.Value = compensation.baseSmoothness;
            baseAnisotropyEdit.Value = compensation.baseAnisotropy;
            baseMetallicEdit.Value = compensation.baseMetallic;

            colorCompensationEdit.IsOn = compensation.colorCompensation;
            enableAnisotropicEdit.IsOn = compensation.enableAnisotropicData;
            autoUpdateEdit.IsOn = compensation.autoUpdateOnChange;

            // Update anisotropy parameter interactability based on enableAnisotropicData
            UpdateAnisotropyInteractability();
            UpdateCompensationInteractability();
        }

        /// <summary>
        /// Hide the shown mesh compensation properties.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            meshCompensation = null;
        }

        /// <summary>
        /// Update the interactability of anisotropy parameters based on whether anisotropic data is enabled.
        /// </summary>
        private void UpdateAnisotropyInteractability()
        {
            bool anisotropicEnabled = meshCompensation.enableAnisotropicData;
            baseSmoothnessEdit.Interactable = anisotropicEnabled;
            baseAnisotropyEdit.Interactable = anisotropicEnabled;
            baseMetallicEdit.Interactable = anisotropicEnabled;
        }
        
        /// <summary>
        /// Update the interactability of color compensation parameters based on whether color compensation is enabled.
        /// </summary>
        private void UpdateCompensationInteractability()
        {
            if (meshCompensation == null) return;

            bool compensationEnabled = meshCompensation.colorCompensation;

            if (minCompensationEdit != null)
                minCompensationEdit.Interactable = compensationEnabled;
            if (maxCompensationEdit != null)
                maxCompensationEdit.Interactable = compensationEnabled;
        }


        private void Awake()
        {
            // Hook up all the value changed events
            minCompensationEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                    meshCompensation.minCompensation = value;
            };

            maxCompensationEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                    meshCompensation.maxCompensation = value;
            };

            lightIntensityEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                    meshCompensation.lightIntensity = value;
            };

            baseSmoothnessEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                    meshCompensation.baseSmoothness = value;
            };

            baseAnisotropyEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                    meshCompensation.baseAnisotropy = value;
            };

            baseMetallicEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                    meshCompensation.baseMetallic = value;
            };

            colorCompensationEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                {
                    meshCompensation.colorCompensation = value;
                    UpdateCompensationInteractability();  // Update min/max compensation sliders
                }
            };

            enableAnisotropicEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                {
                    meshCompensation.enableAnisotropicData = value;
                    UpdateAnisotropyInteractability();

                    // Recalculate compensation when toggling anisotropic data
                    if (value)
                        meshCompensation.CalculateCompensation();
                }
            };

            autoUpdateEdit.OnValueChanged += (value) =>
            {
                if (meshCompensation != null)
                    meshCompensation.autoUpdateOnChange = value;
            };
        }
    }
}