# SimRacing - network setup for the ESP32-S3 control box (W5500 Ethernet, UDP 9999)
#
#  * Opens UDP port 9999 in Windows Firewall for every network profile. The direct cable to the
#    control box is an "Unidentified network" (Public profile), where Windows blocks the game by default.
#  * Removes the BLOCK rules Windows creates when "Cancel" was clicked on the first-run firewall prompt.
#  * Optionally gives the chosen wired adapter the fixed address 192.168.1.100/24 that the firmware expects
#    (control box = 192.168.1.150), or restores that adapter to automatic (DHCP) addressing.
#
# Messages are written without Vietnamese accents on purpose: the Windows console may not show them.

$ErrorActionPreference = 'Stop'
$RuleName = 'SimRacing Buong Lai UDP 9999'
$PcIP = '192.168.1.100'
$BoxIP = '192.168.1.150'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Host 'Can quyen Administrator. Dang mo lai script voi quyen Admin (bam Yes)...' -ForegroundColor Yellow
    Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    exit
}

function Open-Firewall {
    Write-Host ''
    Write-Host '== Tuong lua Windows ==' -ForegroundColor Cyan
    Get-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule -DisplayName $RuleName -Direction Inbound -Protocol UDP -LocalPort 9999 -Action Allow -Profile Any | Out-Null
    Write-Host "  Da mo cong UDP 9999 (moi loai mang: Domain/Private/Public)."
    # Windows adds BLOCK rules for an app when its first-run prompt is cancelled; block beats allow.
    $blocks = Get-NetFirewallApplicationFilter -ErrorAction SilentlyContinue |
        Where-Object { $_.Program -like '*SimRacing.exe' } |
        Get-NetFirewallRule -ErrorAction SilentlyContinue |
        Where-Object { $_.Action -eq 'Block' }
    if ($blocks) {
        $blocks | Remove-NetFirewallRule
        Write-Host "  Da xoa $(@($blocks).Count) luat CHAN SimRacing.exe (do bam Cancel o hop thoai tuong lua truoc day)."
    }
}

function Get-WiredAdapters {
    Get-NetAdapter -Physical -ErrorAction SilentlyContinue |
        Where-Object { $_.Status -eq 'Up' -and $_.MediaType -eq '802.3' -and $_.InterfaceDescription -notmatch 'Wi-?Fi|Wireless|802\.11' }
}

function Show-Adapters {
    $list = @(Get-WiredAdapters)
    Write-Host ''
    Write-Host '== Card mang co day dang cam cap (Ethernet) ==' -ForegroundColor Cyan
    if ($list.Count -eq 0) {
        Write-Host '  Khong thay card Ethernet nao dang co ket noi.' -ForegroundColor Red
        Write-Host '  Kiem tra: cap LAN da cam chac 2 dau, control box da cap nguon 12V, den cong LAN co nhap nhay.'
        return @()
    }
    for ($i = 0; $i -lt $list.Count; $i++) {
        $ips = (Get-NetIPAddress -InterfaceIndex $list[$i].ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | ForEach-Object { $_.IPAddress }) -join ', '
        Write-Host ("  [{0}] {1}  ({2})  IP hien tai: {3}" -f ($i + 1), $list[$i].Name, $list[$i].InterfaceDescription, $(if ($ips) { $ips } else { 'chua co' }))
    }
    return $list
}

function Set-RigAddress {
    $list = @(Show-Adapters)
    if ($list.Count -eq 0) { return }
    $choice = Read-Host "Chon so thu tu card noi voi control box (Enter = bo qua)"
    if (-not $choice) { return }
    $n = 0
    if (-not [int]::TryParse($choice, [ref]$n) -or $n -lt 1 -or $n -gt $list.Count) { Write-Host 'Lua chon khong hop le.' -ForegroundColor Red; return }
    $adapter = $list[$n - 1]
    # Warn when another adapter (often home Wi-Fi) already uses 192.168.1.x: two routes to the same network.
    $clash = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -like '192.168.1.*' -and $_.InterfaceIndex -ne $adapter.ifIndex }
    if ($clash) {
        Write-Host ("  CANH BAO: card '{0}' cung dang dung mang 192.168.1.x ({1})." -f $clash[0].InterfaceAlias, $clash[0].IPAddress) -ForegroundColor Yellow
        Write-Host '  Game van nhan duoc du lieu tu buong lai, nhung nen tat Wi-Fi do khi lai de tranh nham duong.' -ForegroundColor Yellow
    }
    Write-Host ("  Card '{0}' se dung IP tinh {1}/24 (khong co gateway). Card nay se KHONG vao Internet qua router nua." -f $adapter.Name, $PcIP)
    $ok = Read-Host 'Tiep tuc? (y/n)'
    if ($ok -ne 'y') { return }
    Set-NetIPInterface -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -Dhcp Disabled
    Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Remove-NetIPAddress -Confirm:$false
    Get-NetRoute -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false
    New-NetIPAddress -InterfaceIndex $adapter.ifIndex -IPAddress $PcIP -PrefixLength 24 | Out-Null
    Write-Host ("  Da dat {0} = {1}/24." -f $adapter.Name, $PcIP) -ForegroundColor Green
    Start-Sleep -Seconds 3
    Write-Host ("  Thu ping control box {0} ..." -f $BoxIP)
    if (Test-Connection -ComputerName $BoxIP -Count 2 -Quiet) {
        Write-Host '  Control box tra loi ping. Mang da thong!' -ForegroundColor Green
    } else {
        Write-Host '  Control box chua tra loi ping. Kiem tra nguon control box va cap LAN, hoac mo Serial Monitor (115200) xem dong [RIG].' -ForegroundColor Yellow
    }
}

function Restore-Dhcp {
    $list = @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.MediaType -eq '802.3' })
    Write-Host ''
    for ($i = 0; $i -lt $list.Count; $i++) { Write-Host ("  [{0}] {1}  ({2})" -f ($i + 1), $list[$i].Name, $list[$i].InterfaceDescription) }
    $choice = Read-Host 'Chon card can tra ve DHCP (Enter = bo qua)'
    $n = 0
    if (-not $choice -or -not [int]::TryParse($choice, [ref]$n) -or $n -lt 1 -or $n -gt $list.Count) { return }
    $adapter = $list[$n - 1]
    Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Remove-NetIPAddress -Confirm:$false
    Set-NetIPInterface -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -Dhcp Enabled
    Write-Host ("  Card {0} da ve che do IP tu dong (DHCP)." -f $adapter.Name) -ForegroundColor Green
}

Write-Host '================================================================'
Write-Host '  SimRacing - Thiet lap mang cho buong lai ESP32-S3 (UDP 9999)'
Write-Host '================================================================'
Write-Host '  [1] Thiet lap day du: mo tuong lua + dat IP 192.168.1.100 cho card LAN'
Write-Host '  [2] Chi mo tuong lua (card LAN da co IP 192.168.1.x)'
Write-Host '  [3] Tra card LAN ve IP tu dong (DHCP) - dung khi khong lai nua'
Write-Host '  [Q] Thoat'
$mode = Read-Host 'Chon'
try {
    switch ($mode) {
        '1' { Open-Firewall; Set-RigAddress }
        '2' { Open-Firewall; Show-Adapters | Out-Null }
        '3' { Restore-Dhcp }
        default { }
    }
} catch {
    Write-Host ('LOI: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
Read-Host 'Xong. Bam Enter de dong cua so'
