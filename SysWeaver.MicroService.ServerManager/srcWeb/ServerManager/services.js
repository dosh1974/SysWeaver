



async function servicesMain() {

    const user = await getRequest("../Api/auth/GetUser");
    if (user) {
        const ut = user.Tokens;
        if (ut) {
            let ok = false;
            const utl = ut.length;
            for (let i = 0; i < utl; ++i) {
                const t = ut[i];
                ok |= ut === "debug";
                ok |= ut === "admin";
                if (ok)
                    break;
            }
            const b = new ColorIcon("../icons/plus.svg", "IconColorThemeMain", 64, 64, "Click to add a new managed service", async () => {
                b.StartWorking();
                try {
                    const res = await PopUpEdit(
                        "SysWeaver.MicroService.ManagedService",
                        null,
                        "Add",
                        "../icons/plus.svg",
                        "Click to add this new service",
                        "Add new service",
                        "Enter a valid service name below and click add.\nThe name may only contain valid filename characters",
                        "Service name");
                    if (res) {
                        if (!await sendRequest("AddService", res)) {
                            Fail("Failed to create new service. ");
                            return;
                        }
                        Info("Create new managed service");
                    }
                }
                catch (e) {
                    Fail("Failed to create new service. " + e.message);
                }
                finally {
                    b.StopWorking();
                }
            });
            const ss = document.body.getElementsByTagName("ss-add")[0]
            ss.appendChild(b.Element);
            ss.classList.add("Show");

        }
    }


    await Table.addTable("../ServerManager/ServicesTable", document.body.getElementsByTagName("ss-table")[0]);
    PageLoaded();
}