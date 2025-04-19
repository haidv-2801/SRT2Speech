using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.LicenseManager
{
    /// <summary>
    /// Represents the core information stored within a license.
    /// </summary>
    public class LicenseInfo
    {
        /// <summary>
        /// Gets or sets the licensed MAC address (expected format: AA-BB-CC-DD-EE-FF).
        /// </summary>
        public string LicensedMacAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expiration date in UTC.
        /// </summary>
        public DateTime ExpirationDateUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets or sets the customer name (optional).
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;

        // Add other relevant license details if needed
        // public string ProductVersion { get; set; }
        // public LicenseType Type { get; set; } // e.g., Trial, Full

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public LicenseInfo() { }
    }

    // <summary>
    /// Defines the possible outcomes of a license validation check.
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>
        /// License is valid and active.
        /// </summary>
        Valid,

        /// <summary>
        /// License file not found.
        /// </summary>
        FileNotFound,

        /// <summary>
        /// License file is corrupted, tampered with, or has an invalid format/signature.
        /// </summary>
        InvalidOrTampered,

        /// <summary>
        /// The license has expired.
        /// </summary>
        Expired,

        /// <summary>
        /// The license is for a different machine (MAC address mismatch).
        /// </summary>
        WrongMachine,

        /// <summary>
        /// Could not retrieve the hardware ID (MAC address) of the current machine.
        /// </summary>
        HardwareIdError,

        /// <summary>
        /// An unexpected error occurred during validation.
        /// </summary>
        Error
    }

    /// <summary>
    /// Contains the detailed result of a license validation attempt.
    /// </summary>
    public class LicenseResult
    {
        /// <summary>
        /// Gets the overall status of the license validation.
        /// </summary>
        public LicenseStatus Status { get; }

        /// <summary>
        /// Gets a descriptive message about the validation outcome.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the detailed license information if the status is Valid, otherwise null.
        /// </summary>
        public LicenseInfo? LicenseDetails { get; }

        /// <summary>
        /// Gets the exception that occurred during validation, if any.
        /// </summary>
        public Exception? ErrorException { get; }

        /// <summary>
        /// Indicates whether the license is considered valid and usable.
        /// </summary>
        public bool IsValid => Status == LicenseStatus.Valid;

        internal LicenseResult(LicenseStatus status, string message, LicenseInfo? details = null, Exception? exception = null)
        {
            Status = status;
            Message = message;
            LicenseDetails = details;
            ErrorException = exception;
        }
    }
}
