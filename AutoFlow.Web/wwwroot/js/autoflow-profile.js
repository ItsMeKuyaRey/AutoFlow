(function () {
    const page = document.body;
    const currentRole = page.dataset.afCurrentRole || "";
    const roleImage = page.dataset.afRoleImage || "";
    const roleInitials = page.dataset.afRoleInitials || "AF";

    const roleMeta = {
        Administrator: { name: "Main Administrator", description: "System Manager", initials: "AD" },
        ServiceAdvisor: { name: "Service Advisor", description: "Service Advisor", initials: "SA" },
        Technician: { name: "Technician / Mechanic", description: "Technician / Mechanic", initials: "TM" },
        Cashier: { name: "Cashier", description: "Cashier", initials: "CA" }
    };

    const colors = {
        purple: { accent: "#7544f5", dark: "#6336d8", light: "#eee8ff", soft: "rgba(117,68,245,.12)" },
        blue: { accent: "#2563eb", dark: "#1d4ed8", light: "#dbeafe", soft: "rgba(37,99,235,.12)" },
        cyan: { accent: "#0891b2", dark: "#0e7490", light: "#cffafe", soft: "rgba(8,145,178,.12)" },
        emerald: { accent: "#059669", dark: "#047857", light: "#d1fae5", soft: "rgba(5,150,105,.12)" },
        rose: { accent: "#e11d48", dark: "#be123c", light: "#ffe4e6", soft: "rgba(225,29,72,.12)" },
        amber: { accent: "#d97706", dark: "#b45309", light: "#fef3c7", soft: "rgba(217,119,6,.12)" },
        indigo: { accent: "#4f46e5", dark: "#4338ca", light: "#e0e7ff", soft: "rgba(79,70,229,.12)" },
        slate: { accent: "#475569", dark: "#334155", light: "#e2e8f0", soft: "rgba(71,85,105,.12)" }
    };

    const storage = {
        color: "autoflow.appearance.color",
        theme: "autoflow.appearance.theme",
        images: "autoflow.role.profileImages"
    };

    function readImages() {
        try { return JSON.parse(localStorage.getItem(storage.images) || "{}"); }
        catch { return {}; }
    }

    function saveImages(images) {
        try { localStorage.setItem(storage.images, JSON.stringify(images)); }
        catch (e) { alert("The image could not be saved in this browser. Try a smaller image."); }
    }

    function applyColor(name) {
        const color = colors[name] || colors.purple;
        const root = document.documentElement;
        root.style.setProperty("--af-accent", color.accent);
        root.style.setProperty("--af-accent-dark", color.dark);
        root.style.setProperty("--af-accent-light", color.light);
        root.style.setProperty("--af-accent-soft", color.soft);
        localStorage.setItem(storage.color, name);
        document.querySelectorAll(".af-color-option").forEach(el => el.classList.toggle("active", el.dataset.color === name));
    }

    function applyTheme(theme) {
        const value = theme === "dark" ? "dark" : "light";
        document.documentElement.dataset.afTheme = value;
        localStorage.setItem(storage.theme, value);
        document.querySelectorAll(".af-theme-option").forEach(el => el.classList.toggle("active", el.dataset.theme === value));
    }

    function setAvatarElement(element, image, fallback) {
        if (!element) return;
        if (image) {
            element.innerHTML = "";
            const img = document.createElement("img");
            img.src = image;
            img.alt = "Role profile picture";
            element.appendChild(img);
        } else {
            element.textContent = fallback;
        }
    }

    const images = readImages();
    if (!images[currentRole] && roleImage) images[currentRole] = roleImage;

    const drawer = document.getElementById("afProfileDrawer");
    const backdrop = document.getElementById("afProfileBackdrop");
    const trigger = document.getElementById("afProfileTrigger");
    const close = document.getElementById("afProfileClose");
    const selectedAvatar = document.getElementById("afSelectedRoleAvatar");
    const selectedName = document.getElementById("afSelectedRoleName");
    const selectedDescription = document.getElementById("afSelectedRoleDescription");
    const imageInput = document.getElementById("afRoleImageInput");
    const removeImage = document.getElementById("afRemoveRoleImage");
    const tabs = [...document.querySelectorAll(".af-role-tab")];

    let selectedRole = currentRole || "Administrator";

    function refreshRolePanel() {
        const meta = roleMeta[selectedRole] || roleMeta.Administrator;
        const image = images[selectedRole] || "";
        selectedName.textContent = meta.name;
        selectedDescription.textContent = meta.description;
        setAvatarElement(selectedAvatar, image, meta.initials);

        tabs.forEach(tab => {
            tab.classList.toggle("active", tab.dataset.role === selectedRole);
            const avatar = tab.querySelector("[data-avatar-for]");
            if (avatar) setAvatarElement(avatar, images[tab.dataset.role] || "", roleMeta[tab.dataset.role].initials);
        });
    }

    function updateCurrentHeader() {
        const image = images[currentRole] || "";
        const avatar = document.getElementById("afCurrentRoleAvatar");
        const initials = document.getElementById("afCurrentRoleInitials");
        if (avatar) {
            setAvatarElement(avatar.parentElement, image, roleInitials);
        } else if (initials) {
            setAvatarElement(initials.parentElement, image, roleInitials);
        }
    }

    function openDrawer() {
        drawer.classList.add("open");
        backdrop.classList.add("open");
        drawer.setAttribute("aria-hidden", "false");
        refreshRolePanel();
    }

    function closeDrawer() {
        drawer.classList.remove("open");
        backdrop.classList.remove("open");
        drawer.setAttribute("aria-hidden", "true");
    }

    trigger?.addEventListener("click", openDrawer);
    close?.addEventListener("click", closeDrawer);
    backdrop?.addEventListener("click", closeDrawer);
    document.addEventListener("keydown", e => { if (e.key === "Escape") closeDrawer(); });

    tabs.forEach(tab => tab.addEventListener("click", () => {
        selectedRole = tab.dataset.role;
        refreshRolePanel();
    }));

    imageInput?.addEventListener("change", () => {
        const file = imageInput.files?.[0];
        if (!file) return;
        if (file.size > 2 * 1024 * 1024) {
            alert("Please choose an image smaller than 2 MB.");
            imageInput.value = "";
            return;
        }
        const reader = new FileReader();
        reader.onload = () => {
            images[selectedRole] = reader.result;
            saveImages(images);
            refreshRolePanel();
            updateCurrentHeader();
        };
        reader.readAsDataURL(file);
    });

    removeImage?.addEventListener("click", () => {
        delete images[selectedRole];
        saveImages(images);
        refreshRolePanel();
        updateCurrentHeader();
    });

    document.querySelectorAll(".af-color-option").forEach(option => {
        option.addEventListener("click", () => applyColor(option.dataset.color));
    });

    document.querySelectorAll(".af-theme-option").forEach(option => {
        option.addEventListener("click", () => applyTheme(option.dataset.theme));
    });

    document.getElementById("afResetAppearance")?.addEventListener("click", () => {
        localStorage.removeItem(storage.color);
        localStorage.removeItem(storage.theme);
        localStorage.removeItem(storage.images);
        Object.keys(images).forEach(k => delete images[k]);
        applyColor("purple");
        applyTheme("light");
        if (roleImage) images[currentRole] = roleImage;
        refreshRolePanel();
        updateCurrentHeader();
    });

    applyColor(localStorage.getItem(storage.color) || "purple");
    applyTheme(localStorage.getItem(storage.theme) || "light");
    refreshRolePanel();
    updateCurrentHeader();
})();
