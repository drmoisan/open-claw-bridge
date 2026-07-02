using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudAuth;

namespace OpenClaw.Core.Tests.CloudAuth;

/// <summary>
/// Full D5 fail-closed validation matrix for <see cref="CloudAuthOptionsValidator"/>:
/// exactly-one credential source (reject-ambiguous), required tenant/client, scope and
/// authority URI shape, skew range, multi-violation aggregation, and the no-value-echo
/// guarantee — plus CsCheck properties (T1 obligation).
/// </summary>
[TestClass]
public sealed class CloudAuthOptionsValidatorTests
{
    private const string FakeTenantId = "00000000-0000-0000-0000-000000000001";
    private const string FakeClientId = "00000000-0000-0000-0000-000000000002";
    private const string FakeCertificatePath = "/run/secrets/fake-cert.pem";
    private const string FakeClientSecret = "fake-client-secret-value";

    private static CloudAuthOptions ValidCertificateOptions() =>
        new()
        {
            TenantId = FakeTenantId,
            ClientId = FakeClientId,
            CertificatePath = FakeCertificatePath,
        };

    private static CloudAuthOptions ValidSecretOptions() =>
        new()
        {
            TenantId = FakeTenantId,
            ClientId = FakeClientId,
            ClientSecret = FakeClientSecret,
        };

    [TestMethod]
    public void Validate_ValidCertificateOnlyOptions_ReturnsNoViolations()
    {
        // Arrange
        var options = ValidCertificateOptions();

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations.Should().BeEmpty("certificate-only configuration is the preferred source");
    }

    [TestMethod]
    public void Validate_ValidSecretOnlyOptions_ReturnsNoViolations()
    {
        // Arrange
        var options = ValidSecretOptions();

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations.Should().BeEmpty("secret-only configuration is the documented fallback");
    }

    [TestMethod]
    public void Validate_BothCredentialSourcesSet_IsRejectedAsAmbiguous()
    {
        // Arrange
        var options = ValidCertificateOptions();
        options.ClientSecret = FakeClientSecret;

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations
            .Should()
            .ContainSingle(v => v.Contains("both are set"))
            .Which.Should()
            .Contain("Exactly one of CertificatePath or ClientSecret");
    }

    [TestMethod]
    public void Validate_NeitherCredentialSourceSet_IsRejected()
    {
        // Arrange
        var options = new CloudAuthOptions { TenantId = FakeTenantId, ClientId = FakeClientId };

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations
            .Should()
            .ContainSingle(v => v.Contains("neither is set"))
            .Which.Should()
            .Contain("Exactly one of CertificatePath or ClientSecret");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Validate_BlankOrWhitespaceTenantId_IsRejected(string tenantId)
    {
        // Arrange
        var options = ValidCertificateOptions();
        options.TenantId = tenantId;

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations.Should().ContainSingle(v => v.StartsWith("TenantId is required"));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Validate_BlankOrWhitespaceClientId_IsRejected(string clientId)
    {
        // Arrange
        var options = ValidCertificateOptions();
        options.ClientId = clientId;

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations.Should().ContainSingle(v => v.StartsWith("ClientId is required"));
    }

    [DataTestMethod]
    [DataRow("not-a-uri")]
    [DataRow("https://graph.microsoft.com/Mail.Read")]
    public void Validate_MalformedScope_IsRejected(string scope)
    {
        // Arrange: a non-URI value and an absolute URI that lacks the /.default suffix.
        var options = ValidCertificateOptions();
        options.Scope = scope;

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations.Should().ContainSingle(v => v.StartsWith("Scope must be an absolute URI"));
    }

    [DataTestMethod]
    [DataRow("not-a-uri")]
    [DataRow("http://login.microsoftonline.com/")]
    public void Validate_MalformedAuthorityHost_IsRejected(string authorityHost)
    {
        // Arrange: a non-URI value and an absolute URI with a non-https scheme.
        var options = ValidCertificateOptions();
        options.AuthorityHost = authorityHost;

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations
            .Should()
            .ContainSingle(v => v.StartsWith("AuthorityHost must be an absolute https URI"));
    }

    [DataTestMethod]
    [DataRow(-1, false)]
    [DataRow(61, false)]
    [DataRow(0, true)]
    [DataRow(5, true)]
    [DataRow(60, true)]
    public void Validate_RefreshSkewMinutesRange_IsEnforcedInclusively(int skew, bool valid)
    {
        // Arrange
        var options = ValidCertificateOptions();
        options.RefreshSkewMinutes = skew;

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        if (valid)
        {
            violations.Should().BeEmpty($"a skew of {skew} minutes is within [0, 60]");
        }
        else
        {
            violations.Should().ContainSingle(v => v.StartsWith("RefreshSkewMinutes"));
        }
    }

    [TestMethod]
    public void Validate_MultipleSimultaneousViolations_AreAllReported()
    {
        // Arrange: blank tenant + blank client + no credential source + bad scope +
        // bad authority + out-of-range skew = six violations at once.
        var options = new CloudAuthOptions
        {
            TenantId = " ",
            ClientId = "",
            Scope = "not-a-uri",
            AuthorityHost = "http://login.microsoftonline.com/",
            RefreshSkewMinutes = 99,
        };

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations
            .Should()
            .HaveCount(6, "the validator returns the full violation list, not first-failure");
    }

    [TestMethod]
    public void Validate_ViolationMessages_NeverEchoConfiguredValues()
    {
        // Arrange: every property invalid and carrying a distinctive fake value.
        var options = new CloudAuthOptions
        {
            TenantId = " ",
            ClientId = " ",
            CertificatePath = FakeCertificatePath,
            ClientSecret = FakeClientSecret,
            Scope = "fake-scope-value",
            AuthorityHost = "fake-authority-value",
            RefreshSkewMinutes = -1,
        };

        // Act
        var violations = CloudAuthOptionsValidator.Validate(options);

        // Assert
        violations.Should().NotBeEmpty();
        foreach (var violation in violations)
        {
            violation.Should().NotContain(FakeCertificatePath);
            violation.Should().NotContain(FakeClientSecret);
            violation.Should().NotContain("fake-scope-value");
            violation.Should().NotContain("fake-authority-value");
        }
    }

    [TestMethod]
    public void Validate_NullOptions_Throws()
    {
        // Arrange
        var act = () => CloudAuthOptionsValidator.Validate(null!);

        // Act + Assert
        act.Should().Throw<ArgumentNullException>("the validator fails fast on null input");
    }

    /// <summary>
    /// CsCheck property (T1): for arbitrary option mutations, validity implies exactly
    /// one credential source is configured, and violation messages never contain the
    /// configured secret or certificate-path strings.
    /// </summary>
    [TestMethod]
    public void Validate_Property_ValidityImpliesExactlyOneCredentialSourceAndNoValueEcho()
    {
        var genOptions = Gen.Select(
            Gen.OneOfConst("", " ", FakeTenantId),
            Gen.OneOfConst("", " ", FakeClientId),
            Gen.OneOfConst("", FakeCertificatePath),
            Gen.OneOfConst("", FakeClientSecret),
            Gen.Int[-5, 65],
            (tenant, client, certificate, secret, skew) =>
                new CloudAuthOptions
                {
                    TenantId = tenant,
                    ClientId = client,
                    CertificatePath = certificate,
                    ClientSecret = secret,
                    RefreshSkewMinutes = skew,
                }
        );

        genOptions.Sample(
            options =>
            {
                var violations = CloudAuthOptionsValidator.Validate(options);

                var certificateConfigured = !string.IsNullOrWhiteSpace(options.CertificatePath);
                var secretConfigured = !string.IsNullOrWhiteSpace(options.ClientSecret);

                if (violations.Count == 0)
                {
                    (certificateConfigured ^ secretConfigured)
                        .Should()
                        .BeTrue("valid options must configure exactly one credential source");
                }

                foreach (var violation in violations)
                {
                    violation
                        .Should()
                        .NotContain(
                            FakeCertificatePath,
                            "violations must never echo the certificate path"
                        );
                    violation
                        .Should()
                        .NotContain(FakeClientSecret, "violations must never echo the secret");
                }
            },
            iter: 1000
        );
    }
}
