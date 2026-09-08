(function () {
    "use strict";

    const dashboard = document.querySelector("[data-administrator-dashboard]");
    if (!dashboard) return;

    const status = dashboard.querySelector("[data-administrator-status]");
    const search = dashboard.querySelector("[data-administrator-search]");
    const token = dashboard.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";
    const sidebarLinks = Array.from(dashboard.querySelectorAll(".administrator-sidebar [data-admin-nav]"));
    const sections = Array.from(dashboard.querySelectorAll("[data-admin-section]"));
    let noticeTimer;

    const showNotice = (message, isError = false) => {
        if (!status) return;
        window.clearTimeout(noticeTimer);
        status.textContent = message;
        status.hidden = false;
        status.classList.toggle("is-error", isError);
        noticeTimer = window.setTimeout(() => {
            status.hidden = true;
        }, 4200);
    };

    const post = async (url, payload) => {
        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token
            },
            body: payload === undefined ? null : JSON.stringify(payload)
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(result.message || "The database update could not be saved.");
        }
        return result;
    };

    const setActiveNavigation = (name) => {
        sidebarLinks.forEach((link) => {
            const isActive = link.dataset.adminNav === name;
            link.classList.toggle("is-active", isActive);
            if (isActive) link.setAttribute("aria-current", "page");
            else link.removeAttribute("aria-current");
        });
    };

    dashboard.querySelectorAll("[data-admin-nav]").forEach((link) => {
        link.addEventListener("click", () => {
            const name = link.dataset.adminNav;
            const target = document.getElementById(`admin-${name}`);
            if (!target) return;
            setActiveNavigation(name);
            target.scrollIntoView({ behavior: "smooth", block: "start" });
        });
    });

    const date = dashboard.querySelector("[data-administrator-date]");
    if (date) {
        date.dateTime = new Date().toISOString().slice(0, 10);
        date.textContent = new Intl.DateTimeFormat("en-NZ", {
            weekday: "long",
            day: "numeric",
            month: "long",
            year: "numeric"
        }).format(new Date());
    }

    dashboard.querySelector("[data-activity-button]")?.addEventListener("click", () => {
        document.getElementById("admin-activity")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });

    if ("IntersectionObserver" in window) {
        const sectionObserver = new IntersectionObserver((entries) => {
            const visible = entries
                .filter((entry) => entry.isIntersecting)
                .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
            if (visible) setActiveNavigation(visible.target.dataset.adminSection);
        }, { rootMargin: "-20% 0px -62% 0px", threshold: [0, 0.1, 0.35] });
        sections.forEach((section) => sectionObserver.observe(section));

        const revealObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, { rootMargin: "0px 0px -7% 0px", threshold: 0.08 });
        dashboard.querySelectorAll(".administrator-reveal").forEach((item) => revealObserver.observe(item));
    } else {
        dashboard.querySelectorAll(".administrator-reveal").forEach((item) => item.classList.add("is-visible"));
    }

    const animateCount = (element) => {
        const target = Number(element.dataset.adminCount);
        if (!Number.isFinite(target)) return;
        const duration = 950;
        const startedAt = performance.now();
        const frame = (now) => {
            const progress = Math.min((now - startedAt) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = Math.round(target * eased).toLocaleString("en-NZ");
            if (progress < 1) requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    };
    dashboard.querySelectorAll("[data-admin-count]").forEach(animateCount);

    const filterPlatformItems = () => {
        if (!search) return;
        const query = search.value.trim().toLocaleLowerCase();
        let matches = 0;
        dashboard.querySelectorAll("[data-admin-search-item]").forEach((item) => {
            const match = !query || item.textContent.toLocaleLowerCase().includes(query);
            item.classList.toggle("is-filtered-out", !match);
            if (match && query) matches += 1;
        });
        if (query) showNotice(`${matches} database result${matches === 1 ? "" : "s"} found for “${search.value.trim()}”.`);
    };
    search?.addEventListener("input", filterPlatformItems);

    const userSearch = dashboard.querySelector("[data-user-search]");
    const userRoleFilter = dashboard.querySelector("[data-user-role-filter]");
    const filterUsers = () => {
        const query = userSearch?.value.trim().toLocaleLowerCase() || "";
        const role = userRoleFilter?.value || "all";
        dashboard.querySelectorAll("[data-user-row]").forEach((row) => {
            const matchesQuery = !query || row.textContent.toLocaleLowerCase().includes(query);
            const matchesRole = role === "all" || row.dataset.userRole === role;
            row.classList.toggle("is-user-filtered", !matchesQuery || !matchesRole);
        });
    };
    userSearch?.addEventListener("input", filterUsers);
    userRoleFilter?.addEventListener("change", filterUsers);

    document.addEventListener("keydown", (event) => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLocaleLowerCase() === "k") {
            event.preventDefault();
            search?.focus();
        }
    });

    dashboard.querySelectorAll("[data-merchant-action]").forEach((button) => {
        button.addEventListener("click", async () => {
            const card = button.closest("[data-merchant-application]");
            if (!card) return;
            const requestedStatus = button.dataset.merchantAction;
            if (requestedStatus === "Rejected" && !window.confirm(`Reject ${card.dataset.merchantName}'s application?`)) return;

            const actionButtons = Array.from(card.querySelectorAll("[data-merchant-action]"));
            actionButtons.forEach((item) => { item.disabled = true; });
            try {
                const result = await post(`/api/platform/merchant-applications/${card.dataset.applicationId}/status`, { status: requestedStatus });
                const badge = card.querySelector("[data-merchant-status]");
                if (badge) {
                    badge.textContent = result.status;
                    badge.className = `administrator-badge administrator-badge--${result.status === "Approved" ? "active" : "inactive"}`;
                }
                showNotice(result.message);
                window.setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                actionButtons.forEach((item) => { item.disabled = false; });
                showNotice(error.message, true);
            }
        });
    });

    dashboard.querySelectorAll("[data-product-toggle]").forEach((button) => {
        button.addEventListener("click", async () => {
            const card = button.closest("[data-product-row]");
            if (!card) return;
            button.disabled = true;
            try {
                const result = await post(`/api/platform/products/${card.dataset.productId}/availability`);
                const badge = card.querySelector("[data-product-status]");
                if (badge) {
                    badge.textContent = result.isActive ? "Active" : "Disabled";
                    badge.className = `administrator-badge administrator-badge--${result.isActive ? "active" : "inactive"}`;
                }
                button.textContent = result.isActive ? "Disable" : "Enable";
                showNotice(result.message);
            } catch (error) {
                showNotice(error.message, true);
            } finally {
                button.disabled = false;
            }
        });
    });

    dashboard.querySelectorAll("[data-order-archive]").forEach((button) => {
        button.addEventListener("click", async () => {
            const row = button.closest("[data-order-row]");
            if (!row || !window.confirm("Archive this order record?")) return;
            button.disabled = true;
            try {
                const result = await post(`/api/platform/orders/${row.dataset.orderId}/archive`);
                const badge = row.querySelector("[data-order-status]");
                if (badge) {
                    badge.textContent = "Archived";
                    badge.className = "administrator-badge administrator-badge--inactive";
                }
                row.classList.add("is-archived");
                button.textContent = "Archived";
                showNotice(result.message);
            } catch (error) {
                button.disabled = false;
                showNotice(error.message, true);
            }
        });
    });

    dashboard.querySelectorAll("[data-review-action]").forEach((button) => {
        button.addEventListener("click", async () => {
            const row = button.closest("[data-review-row]");
            if (!row) return;
            const requestedStatus = button.dataset.reviewAction;
            button.disabled = true;
            try {
                const result = await post(`/api/platform/reviews/${row.dataset.reviewId}/status`, { status: requestedStatus });
                const badge = row.querySelector("[data-review-status]");
                if (badge) {
                    badge.textContent = result.status;
                    badge.className = `administrator-badge administrator-badge--${result.status.toLocaleLowerCase()}`;
                }
                button.dataset.reviewAction = result.status === "Active" ? "Hidden" : "Active";
                button.textContent = result.status === "Active" ? "Hide" : "Publish";
                showNotice(result.message);
            } catch (error) {
                showNotice(error.message, true);
            } finally {
                button.disabled = false;
            }
        });
    });

    dashboard.querySelector("[data-administrator-logout]")?.addEventListener("click", async (event) => {
        event.preventDefault();
        try {
            const result = await post("/api/auth/logout");
            window.location.assign(result.redirectUrl);
        } catch (error) {
            showNotice(error.message, true);
        }
    });
}());
