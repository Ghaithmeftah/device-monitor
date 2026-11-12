// MVVM dashboard: KnockoutJS view models bound to the view, kept in sync by the REST API and SignalR pushes.
(function () {
    "use strict";

    function DeviceViewModel(dto) {
        this.id = dto.id;
        this.name = dto.name;
        this.serialNumber = dto.serialNumber;
        this.type = dto.type;
        this.status = ko.observable();
        this.lastSeenUtc = ko.observable();
        this.isOnline = ko.pureComputed(() => this.status() === "Online");
        this.lastSeenText = ko.pureComputed(() =>
            this.lastSeenUtc() ? new Date(this.lastSeenUtc()).toLocaleString() : "Never");
        this.update(dto);
    }

    DeviceViewModel.prototype.update = function (dto) {
        this.status(dto.status);
        this.lastSeenUtc(dto.lastSeenUtc);
    };

    function DashboardViewModel() {
        const self = this;
        self.devices = ko.observableArray([]);
        self.deviceTypes = ["Gateway", "SetTopBox", "SmartMeter"];
        self.newName = ko.observable("");
        self.newSerial = ko.observable("");
        self.newType = ko.observable("Gateway");
        self.error = ko.observable("");
        self.busy = ko.observable(false);
        self.connectionState = ko.observable("Connecting");

        self.onlineCount = ko.pureComputed(() => self.devices().filter(d => d.isOnline()).length);
        self.offlineCount = ko.pureComputed(() => self.devices().length - self.onlineCount());

        // Applies a device sent by the API or by SignalR; the same change can arrive from both, so it must be idempotent.
        self.upsert = function (dto) {
            const existing = self.devices().find(d => d.id === dto.id);
            if (existing) {
                existing.update(dto);
            } else {
                self.devices.push(new DeviceViewModel(dto));
                self.devices.sort((a, b) => a.name.localeCompare(b.name));
            }
        };

        self.removeById = function (id) {
            self.devices.remove(d => d.id === id);
        };

        self.load = async function () {
            const devices = await api("GET", "/api/devices");
            self.devices(devices.map(d => new DeviceViewModel(d)));
        };

        self.addDevice = async function () {
            self.busy(true);
            self.error("");
            try {
                self.upsert(await api("POST", "/api/devices", {
                    name: self.newName(), serialNumber: self.newSerial(), type: self.newType()
                }));
                self.newName("");
                self.newSerial("");
            } catch (e) {
                self.error(e.message);
            } finally {
                self.busy(false);
            }
        };

        self.sendHeartbeat = async function (device) {
            try {
                self.upsert(await api("POST", `/api/devices/${device.id}/heartbeat`));
            } catch (e) {
                self.error(e.message);
            }
        };

        self.removeDevice = async function (device) {
            if (!confirm(`Delete ${device.name}?`)) return;
            try {
                await api("DELETE", `/api/devices/${device.id}`);
                self.removeById(device.id);
            } catch (e) {
                self.error(e.message);
            }
        };
    }

    async function api(method, url, body) {
        const response = await fetch(url, {
            method,
            headers: body ? { "Content-Type": "application/json" } : {},
            body: body ? JSON.stringify(body) : undefined
        });
        if (!response.ok) {
            const problem = await response.json().catch(() => null);
            const details = problem && problem.errors ? Object.values(problem.errors).flat().join(" ") : "";
            throw new Error(details || (problem && problem.title) || `Request failed (${response.status})`);
        }
        return response.status === 204 ? null : response.json();
    }

    const vm = new DashboardViewModel();
    ko.applyBindings(vm);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/devices")
        .withAutomaticReconnect()
        .build();
    connection.on("DeviceUpdated", vm.upsert);
    connection.on("DeviceDeleted", vm.removeById);
    connection.onreconnecting(() => vm.connectionState("Reconnecting"));
    connection.onreconnected(() => { vm.connectionState("Live"); vm.load(); });
    connection.onclose(() => vm.connectionState("Offline"));

    vm.load().catch(e => vm.error(e.message));
    connection.start()
        .then(() => vm.connectionState("Live"))
        .catch(() => vm.connectionState("Offline"));
})();
