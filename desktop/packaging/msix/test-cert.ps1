# SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
# SPDX-License-Identifier: MIT
#
# A self-signed certificate for testing the Store package on this machine
# (test installs of the Store package). Windows installs a package only when it is
# signed by a certificate it trusts, and whose subject is the package's
# publisher: the Store's CN, as Partner Center reserved it. The Store signs
# the real package itself; this certificate never leaves this machine.
#
# Created once into -Pfx (with a .cer beside it) and trusted in
# LocalMachine\TrustedPeople, which asks for administrator rights once.
param(
    [Parameter(Mandatory = $true)][string]$Pfx,
    [Parameter(Mandatory = $true)][string]$Password
)
$ErrorActionPreference = 'Stop'

$subject = 'CN=697C0CE1-99F6-4E24-B7D7-85A183DB58F0'
if (Test-Path $Pfx) { exit 0 }

New-Item -ItemType Directory -Force -Path (Split-Path $Pfx) | Out-Null
$cert = New-SelfSignedCertificate -Type Custom -Subject $subject `
    -KeyUsage DigitalSignature -FriendlyName 'Raidex Keys Store package test' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $Pfx -Password $secure | Out-Null
$cer = [IO.Path]::ChangeExtension($Pfx, '.cer')
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

Write-Host 'Trusting the test certificate (asks for administrator rights) ...'
$import = "Import-Certificate -FilePath '$cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"
$p = Start-Process powershell -Verb RunAs -Wait -PassThru -ArgumentList '-NoProfile', '-Command', $import
if ($p.ExitCode -ne 0) {
    Remove-Item $Pfx, $cer
    throw 'The certificate could not be trusted.'
}
