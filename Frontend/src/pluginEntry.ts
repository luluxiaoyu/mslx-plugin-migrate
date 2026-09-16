import SettingsMigrationCard from './views/SettingsMigrationCard.vue';
import './style.css';

export const pluginConfig = {
    name: 'MSLXMigrationPlugin',
    version: '1.0.0',
    routes: [],
    extensions: [
        {
            slot: 'settings-daemon-bottom',
            component: SettingsMigrationCard,
        }
    ]
};